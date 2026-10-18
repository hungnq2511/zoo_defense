using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// All economy operations on a PlayerState. Every method takes the current time as unix seconds
    /// so the logic is testable and can run unchanged on a server. Methods return false (or an empty
    /// list) when the action is not allowed; nothing is changed in that case.
    /// </summary>
    public sealed class MetaService
    {
        public const int SecondsPerDay = 86400;

        public readonly MetaConfig Config;
        public readonly ContentDatabase Content;
        public readonly PlayerState State;

        readonly Dictionary<string, CardState> _cards = new Dictionary<string, CardState>();
        readonly List<int>[] _unitsByRarity = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        public MetaService(MetaConfig config, ContentDatabase content, PlayerState state)
        {
            Config = config;
            Content = content;
            State = state;
            for (int i = 0; i < content.Units.Count; i++) _unitsByRarity[(int)content.Units[i].rarity].Add(i);
            Normalize();
        }

        /// <summary>A fresh account: every Common plus the default deck unlocked at level 1.</summary>
        public static PlayerState NewPlayer(MetaConfig config, ContentDatabase content, int seed)
        {
            var s = new PlayerState { gold = config.startGold, gems = config.startGems, rng = new DetRandom(seed).State };
            for (int i = 0; i < content.Units.Count; i++)
            {
                var u = content.Units[i];
                bool unlocked = u.rarity == Rarity.Common || Array.IndexOf(DefaultContent.DefaultDeck, u.id) >= 0;
                s.cards.Add(new CardState { id = u.id, level = unlocked ? 1 : 0 });
            }
            return s;
        }

        void Normalize()
        {
            // Tolerate saves from older versions or new content.
            if (State.cards == null) State.cards = new List<CardState>();
            foreach (var c in State.cards) if (c != null && c.id != null) _cards[c.id] = c;
            for (int i = 0; i < Content.Units.Count; i++)
            {
                var id = Content.Units[i].id;
                if (!_cards.ContainsKey(id)) { var c = new CardState { id = id }; State.cards.Add(c); _cards[id] = c; }
            }
            if (State.chests == null || State.chests.Length != Config.chestSlots)
            {
                var old = State.chests ?? new ChestSlot[0];
                State.chests = new ChestSlot[Config.chestSlots];
                for (int i = 0; i < State.chests.Length; i++) State.chests[i] = i < old.Length && old[i] != null ? old[i] : new ChestSlot();
            }
            for (int i = 0; i < State.chests.Length; i++) if (State.chests[i] == null) State.chests[i] = new ChestSlot();
            if (State.missions == null) State.missions = new List<MissionState>();
            if (State.passClaimedFree == null) State.passClaimedFree = new List<int>();
            if (State.passClaimedPremium == null) State.passClaimedPremium = new List<int>();
            if (State.dealUnits == null) State.dealUnits = new List<string>();
            if (State.dealBought == null) State.dealBought = new List<bool>();
            if (State.rng == 0) State.rng = new DetRandom(State.trophies + 12345).State;
        }

        // ------------------------------------------------------------------ helpers

        DetRandom Rng() { return DetRandom.FromState(State.rng); }
        void Save(ref DetRandom r) { State.rng = r.State; }

        public static int Day(long now) { return (int)(now / SecondsPerDay); }

        public int ArenaIndex()
        {
            int a = 1;
            for (int i = 0; i < Config.arenaTrophies.Length; i++) if (State.trophies >= Config.arenaTrophies[i]) a = i + 1;
            return a;
        }

        /// <summary>Daily resets (missions, shop) and season changes. Call before showing meta screens.</summary>
        public void Refresh(long now)
        {
            int day = Day(now);
            if (State.missionsDay != day) RollMissions(day);
            if (State.shopDay != day) RollDeals(day);
            int season = Season(now);
            if (State.passSeason != season)
            {
                State.passSeason = season;
                State.passXp = 0;
                State.passPremium = false;
                State.passClaimedFree.Clear();
                State.passClaimedPremium.Clear();
            }
        }

        // ------------------------------------------------------------------ cards

        public CardState Card(string unitId) { CardState c; return _cards.TryGetValue(unitId, out c) ? c : null; }
        public int Level(string unitId) { var c = Card(unitId); return c == null ? 0 : c.level; }
        public bool IsUnlocked(string unitId) { return Level(unitId) > 0; }

        public int[] DeckLevels(string[] deck)
        {
            var levels = new int[deck.Length];
            for (int i = 0; i < deck.Length; i++) levels[i] = Math.Max(1, Level(deck[i]));
            return levels;
        }

        public bool UpgradeCost(string unitId, out int shards, out int gold)
        {
            shards = gold = 0;
            var c = Card(unitId);
            var def = Content.Unit(unitId);
            if (c == null || def == null || c.level < 1 || c.level >= Config.maxCardLevel) return false;
            shards = Config.UpgradeShards(def.rarity, c.level);
            gold = Config.UpgradeGold(c.level);
            return true;
        }

        public bool CanUpgrade(string unitId)
        {
            int shards, gold;
            var c = Card(unitId);
            return UpgradeCost(unitId, out shards, out gold) && c.shards >= shards && State.gold >= gold;
        }

        public bool Upgrade(string unitId)
        {
            int shards, gold;
            if (!CanUpgrade(unitId)) return false;
            UpgradeCost(unitId, out shards, out gold);
            var c = Card(unitId);
            c.shards -= shards;
            State.gold -= gold;
            c.level++;
            Progress(MissionType.UpgradeUnits, 1);
            return true;
        }

        /// <summary>Adds shards; the first shard of a locked animal unlocks it at level 1.</summary>
        void AddShards(string unitId, int amount)
        {
            var c = Card(unitId);
            if (c == null || amount <= 0) return;
            if (c.level == 0) { c.level = 1; amount--; }
            c.shards += amount;
        }

        // ------------------------------------------------------------------ rewards

        /// <summary>Applies one reward. Chests are opened on the spot and expanded into gold and shards.</summary>
        public void Grant(Reward r, List<Reward> log)
        {
            switch (r.type)
            {
                case RewardType.Gold: State.gold += r.amount; log.Add(r); break;
                case RewardType.Gems: State.gems += r.amount; log.Add(r); break;
                case RewardType.Shards: AddShards(r.id, r.amount); log.Add(r); break;
                case RewardType.PassXp: State.passXp += r.amount; log.Add(r); break;
                case RewardType.Chest:
                    var def = Config.Chest(r.id);
                    if (def == null) return;
                    for (int i = 0; i < Math.Max(1, r.amount); i++) foreach (var x in RollChest(def)) Grant(x, log);
                    Progress(MissionType.OpenChests, Math.Max(1, r.amount));
                    break;
            }
        }

        public static List<Reward> Merge(List<Reward> rewards)
        {
            var merged = new List<Reward>();
            foreach (var r in rewards)
            {
                int i = merged.FindIndex(m => m.type == r.type && m.id == r.id);
                if (i < 0) merged.Add(r);
                else { var m = merged[i]; m.amount += r.amount; merged[i] = m; }
            }
            return merged;
        }

        List<Reward> RollChest(ChestDef def)
        {
            var rng = Rng();
            int mult = 100 + Config.arenaBonusPercent * (ArenaIndex() - 1);
            var list = new List<Reward>();
            int gold = (def.goldMin + rng.Next(def.goldMax - def.goldMin + 1)) * mult / 100;
            list.Add(new Reward(RewardType.Gold, gold));

            int shards = def.shards * mult / 100;
            if (rng.Chance(def.legendaryChance)) { list.Add(Shards(ref rng, Rarity.Legendary, 1)); }
            if (rng.Chance(def.epicChance)) { int e = Math.Max(1, shards / 16); list.Add(Shards(ref rng, Rarity.Epic, e)); shards -= e; }
            int rare = shards * 30 / 100;
            int rareStacks = rare >= 6 ? 2 : 1;
            for (int i = 0; i < rareStacks && rare > 0; i++) list.Add(Shards(ref rng, Rarity.Rare, i == rareStacks - 1 ? rare - rare / rareStacks * i : rare / rareStacks));
            int common = shards - rare;
            int commonStacks = common >= 20 ? 3 : 2;
            for (int i = 0; i < commonStacks && common > 0; i++) list.Add(Shards(ref rng, Rarity.Common, i == commonStacks - 1 ? common - common / commonStacks * i : common / commonStacks));
            Save(ref rng);
            return list;
        }

        Reward Shards(ref DetRandom rng, Rarity rarity, int amount)
        {
            var pool = _unitsByRarity[(int)rarity];
            var unit = Content.Units[pool[rng.Next(pool.Count)]];
            return new Reward(RewardType.Shards, Math.Max(1, amount), unit.id);
        }

        // ------------------------------------------------------------------ chests

        public ChestStatus Status(int slot, long now)
        {
            var c = State.chests[slot];
            if (string.IsNullOrEmpty(c.chestId)) return ChestStatus.Empty;
            if (c.unlockEndsAt == 0) return ChestStatus.Locked;
            return now >= c.unlockEndsAt ? ChestStatus.Ready : ChestStatus.Unlocking;
        }

        public bool IsAnyUnlocking(long now)
        {
            for (int i = 0; i < State.chests.Length; i++) if (Status(i, now) == ChestStatus.Unlocking) return true;
            return false;
        }

        public long SecondsLeft(int slot, long now)
        {
            var c = State.chests[slot];
            var def = Config.Chest(c.chestId);
            if (def == null) return 0;
            if (c.unlockEndsAt == 0) return def.unlockSeconds;
            return Math.Max(0, c.unlockEndsAt - now);
        }

        public int SkipCost(int slot, long now)
        {
            long left = SecondsLeft(slot, now);
            if (left <= 0) return 0;
            long minutes = (left + 59) / 60;
            return (int)Math.Max(1, (minutes + Config.skipGemsPerMinutes - 1) / Config.skipGemsPerMinutes);
        }

        /// <summary>Starts the timer. Only one chest unlocks at a time.</summary>
        public bool StartUnlock(int slot, long now)
        {
            if (Status(slot, now) != ChestStatus.Locked || IsAnyUnlocking(now)) return false;
            State.chests[slot].unlockEndsAt = now + Config.Chest(State.chests[slot].chestId).unlockSeconds;
            return true;
        }

        /// <summary>Opens a ready chest, or pays gems to open a locked/unlocking one now.</summary>
        public List<Reward> OpenChest(int slot, long now, bool payGems)
        {
            var status = Status(slot, now);
            if (status == ChestStatus.Empty) return null;
            if (status != ChestStatus.Ready)
            {
                if (!payGems) return null;
                int cost = SkipCost(slot, now);
                if (State.gems < cost) return null;
                State.gems -= cost;
            }
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Chest, 1, State.chests[slot].chestId), log);
            State.chests[slot] = new ChestSlot();
            return Merge(log);
        }

        bool TryAddChest(string chestId)
        {
            for (int i = 0; i < State.chests.Length; i++)
                if (string.IsNullOrEmpty(State.chests[i].chestId)) { State.chests[i] = new ChestSlot { chestId = chestId }; return true; }
            return false;
        }

        // ------------------------------------------------------------------ matches

        public MatchRewards ApplyMatch(MatchSummary m, long now)
        {
            Refresh(now);
            var r = new MatchRewards();
            bool won = m.winner == 0, draw = m.winner == -1;
            if (m.ranked && !draw) r.trophies = won ? Config.winTrophies : -Math.Min(Config.lossTrophies, State.trophies);
            r.gold = draw ? Config.drawGold : (won ? Config.winGold : Config.lossGold);
            r.passXp = won ? Config.winPassXp : Config.lossPassXp;

            // Ranked win streak and revenge bonuses
            if (m.ranked && won) State.winStreak++;
            else if (m.ranked && !draw) State.winStreak = 0;
            r.winStreak = State.winStreak;
            if (won)
            {
                int pct = 0;
                if (m.ranked && State.winStreak >= 2) pct += Math.Min(Config.streakBonusMaxPercent, Config.streakBonusPercent * (State.winStreak - 1));
                if (m.revenge) pct += Config.revengeBonusPercent;
                r.bonusGold = Config.winGold * pct / 100;
                r.gold += r.bonusGold;
            }
            if (!m.ranked) r.passXp /= 2;

            if (won && m.ranked)
            {
                string chest = Config.chestCycle[State.chestCycle % Config.chestCycle.Length];
                if (TryAddChest(chest)) { r.chestId = chest; State.chestCycle++; }
                else r.chestSlotsFull = true;
            }

            State.trophies += r.trophies;
            State.gold += r.gold;
            State.passXp += r.passXp;

            Progress(MissionType.PlayMatches, 1);
            if (won) Progress(MissionType.WinMatches, 1);
            Progress(MissionType.KillEnemies, m.kills);
            Progress(MissionType.MergeUnits, m.merges);
            Progress(MissionType.SendMonsters, m.sends);
            Progress(MissionType.BuyCards, m.cardsBought);
            ProgressMax(MissionType.ReachWave, m.wave);
            return r;
        }

        // ------------------------------------------------------------------ missions

        void RollMissions(int day)
        {
            State.missionsDay = day;
            State.missions.Clear();
            var rng = new DetRandom(day * 7919 + (int)(State.rng & 0xFFFF));
            var pool = new List<MissionDef>(Config.missionPool);
            for (int i = 0; i < Config.missionsPerDay && pool.Count > 0; i++)
            {
                int pick = rng.Next(pool.Count);
                State.missions.Add(new MissionState { id = pool[pick].id });
                pool.RemoveAt(pick);
            }
        }

        void Progress(MissionType type, int amount)
        {
            if (amount <= 0) return;
            foreach (var ms in State.missions)
            {
                var def = Config.Mission(ms.id);
                if (def != null && def.type == type && !ms.claimed) ms.progress = Math.Min(def.target, ms.progress + amount);
            }
        }

        void ProgressMax(MissionType type, int value)
        {
            foreach (var ms in State.missions)
            {
                var def = Config.Mission(ms.id);
                if (def != null && def.type == type && !ms.claimed) ms.progress = Math.Min(def.target, Math.Max(ms.progress, value));
            }
        }

        public int ClaimableMissions()
        {
            int n = 0;
            foreach (var ms in State.missions) { var d = Config.Mission(ms.id); if (d != null && !ms.claimed && ms.progress >= d.target) n++; }
            return n;
        }

        public List<Reward> ClaimMission(int index)
        {
            if (index < 0 || index >= State.missions.Count) return null;
            var ms = State.missions[index];
            var def = Config.Mission(ms.id);
            if (def == null || ms.claimed || ms.progress < def.target) return null;
            ms.claimed = true;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gold, def.gold), log);
            Grant(new Reward(RewardType.PassXp, def.passXp), log);
            return log;
        }

        // ------------------------------------------------------------------ season pass

        public int Season(long now) { return (int)Math.Max(0, (now - Config.seasonEpochUnix) / ((long)Config.seasonDays * SecondsPerDay)); }

        public long SeasonSecondsLeft(long now)
        {
            long len = (long)Config.seasonDays * SecondsPerDay;
            return Config.seasonEpochUnix + (Season(now) + 1) * len - now;
        }

        public int PassTier { get { return Math.Min(Config.passTiers.Count, State.passXp / Config.passXpPerTier); } }

        public bool CanClaimPass(int tier, bool premium)
        {
            if (tier < 1 || tier > PassTier) return false;
            if (premium && !State.passPremium) return false;
            return !(premium ? State.passClaimedPremium : State.passClaimedFree).Contains(tier);
        }

        public int ClaimablePass()
        {
            int n = 0;
            for (int t = 1; t <= PassTier; t++) { if (CanClaimPass(t, false)) n++; if (CanClaimPass(t, true)) n++; }
            return n;
        }

        public List<Reward> ClaimPass(int tier, bool premium)
        {
            if (!CanClaimPass(tier, premium)) return null;
            (premium ? State.passClaimedPremium : State.passClaimedFree).Add(tier);
            var t = Config.passTiers[tier - 1];
            var log = new List<Reward>();
            Grant(premium ? t.premium : t.free, log);
            return Merge(log);
        }

        public bool BuyPremiumPass()
        {
            if (State.passPremium || State.gems < Config.passPremiumGems) return false;
            State.gems -= Config.passPremiumGems;
            State.passPremium = true;
            return true;
        }

        // ------------------------------------------------------------------ shop

        void RollDeals(int day)
        {
            State.shopDay = day;
            State.dealUnits.Clear();
            State.dealBought.Clear();
            var rng = new DetRandom(day * 104729 + (int)(State.rng & 0xFFFF));
            for (int i = 0; i < Config.dailyDeals; i++)
            {
                var rarity = (Rarity)Math.Min(i, 2);            // one Common, one Rare, one Epic
                var pool = _unitsByRarity[(int)rarity];
                State.dealUnits.Add(Content.Units[pool[rng.Next(pool.Count)]].id);
                State.dealBought.Add(false);
            }
        }

        public void DealPrice(int index, out int shards, out int gold)
        {
            var rarity = (int)Content.Unit(State.dealUnits[index]).rarity;
            shards = Config.dealShards[rarity];
            gold = Config.dealGold[rarity];
        }

        public List<Reward> BuyDeal(int index)
        {
            if (index < 0 || index >= State.dealUnits.Count || State.dealBought[index]) return null;
            int shards, gold;
            DealPrice(index, out shards, out gold);
            if (State.gold < gold) return null;
            State.gold -= gold;
            State.dealBought[index] = true;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Shards, shards, State.dealUnits[index]), log);
            return log;
        }

        public List<Reward> BuyChest(string chestId)
        {
            var def = Config.Chest(chestId);
            if (def == null || def.gemPrice <= 0 || State.gems < def.gemPrice) return null;
            State.gems -= def.gemPrice;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Chest, 1, chestId), log);
            return Merge(log);
        }

        public bool BuyGold(bool big)
        {
            int gems = big ? Config.goldPackBigGems : Config.goldPackSmallGems;
            if (State.gems < gems) return false;
            State.gems -= gems;
            State.gold += big ? Config.goldPackBigGold : Config.goldPackSmallGold;
            return true;
        }

        public bool CanClaimFreeGold(long now) { return State.freeGoldDay != Day(now); }

        public bool ClaimFreeGold(long now)
        {
            if (!CanClaimFreeGold(now)) return false;
            State.freeGoldDay = Day(now);
            State.gold += Config.freeDailyGold;
            return true;
        }

        public bool CanClaimAdGems(long now) { return State.adGemsDay != Day(now); }

        public bool ClaimAdGems(long now)
        {
            if (!CanClaimAdGems(now)) return false;
            State.adGemsDay = Day(now);
            State.gems += Config.adGems;
            return true;
        }

        /// <summary>Call after the store confirms a purchase (later: after server receipt validation).</summary>
        public bool GrantGemPack(string productId)
        {
            foreach (var p in Config.gemPacks) if (p.id == productId) { State.gems += p.gems; return true; }
            return false;
        }

        // ------------------------------------------------------------------ gacha

        public List<Reward> Pull(int count)
        {
            int price = count >= 10 ? Config.gachaPrice10 : Config.gachaPrice * count;
            if (count <= 0 || State.gems < price) return null;
            State.gems -= price;
            var rng = Rng();
            var rolls = new List<Reward>();
            for (int i = 0; i < count; i++)
            {
                Rarity rarity;
                State.gachaPity++;
                if (State.gachaPity >= Config.gachaPity) rarity = Rarity.Legendary;
                else
                {
                    int roll = rng.Next(100), acc = 0;
                    rarity = Rarity.Common;
                    for (int r = 0; r < 4; r++) { acc += Config.gachaRarityWeights[r]; if (roll < acc) { rarity = (Rarity)r; break; } }
                }
                if (rarity == Rarity.Legendary) State.gachaPity = 0;
                rolls.Add(Shards(ref rng, rarity, Config.gachaShards[(int)rarity]));
            }
            Save(ref rng);
            var log = new List<Reward>();
            foreach (var r in rolls) Grant(r, log);
            return log;   // not merged: the UI shows each pull
        }
    }
}
