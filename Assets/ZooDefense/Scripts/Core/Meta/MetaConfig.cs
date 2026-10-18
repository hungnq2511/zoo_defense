using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    // Out-of-match economy: chests, card upgrades, shop, gacha, missions and the season pass.
    // Pure C# like the match simulation, so the same rules can run in Cloud Code later
    // (the server becomes the authority; the client keeps these for instant feedback).

    public enum Currency { Gold = 0, Gems = 1 }

    public enum RewardType { Gold = 0, Gems = 1, Shards = 2, Chest = 3, PassXp = 4 }

    [Serializable]
    public struct Reward
    {
        public RewardType type;
        public string id;      // unit id for Shards, chest id for Chest
        public int amount;

        public Reward(RewardType type, int amount, string id = null) { this.type = type; this.amount = amount; this.id = id; }
    }

    [Serializable]
    public class ChestDef
    {
        public string id;
        public int unlockSeconds;
        public int goldMin, goldMax;
        public int shards;            // total shards in the chest (before arena bonus)
        public int epicChance;        // % chance to contain epic shards
        public int legendaryChance;   // % chance to contain one legendary shard
        public int gemPrice;          // shop price, 0 = not sold
    }

    public enum MissionType { PlayMatches, WinMatches, KillEnemies, MergeUnits, SendMonsters, ReachWave, BuyCards, OpenChests, UpgradeUnits, EffectiveHits, KillBosses, PlayModes, DailyChallenge }

    [Serializable]
    public class MissionDef
    {
        public string id;
        public MissionType type;
        public int target;
        public int gold;
        public int passXp;
    }

    [Serializable]
    public class PassTier
    {
        public Reward free;
        public Reward premium;
    }

    [Serializable]
    public class GemPack
    {
        public string id;          // store product id
        public int gems;
        public string price;       // display only; real price comes from the store
    }

    [Serializable]
    public class MetaConfig
    {
        // Cards
        public int maxCardLevel = 15;
        /// <summary>Shards to go from level L to L+1 for a Common card (index L-1). Other rarities divide by rarityShardDivisor.</summary>
        public int[] upgradeShards = { 2, 4, 8, 16, 30, 50, 80, 120, 180, 250, 350, 500, 700, 1000 };
        public int[] rarityShardDivisor = { 1, 3, 8, 20 };   // Rare cards need a third of the shards, etc. (rounded up)
        public int[] upgradeGold = { 20, 50, 100, 200, 400, 700, 1000, 1500, 2200, 3000, 4000, 5500, 7000, 9000 };

        // Chests
        public int chestSlots = 4;
        public List<ChestDef> chests = new List<ChestDef>();
        public string[] chestCycle = { "wood", "wood", "silver", "wood", "silver", "wood", "gold", "wood", "silver", "wood", "wood", "magic" };
        public int skipGemsPerMinutes = 6;   // 1 gem per 6 minutes left
        public int arenaBonusPercent = 25;   // more gold and shards per arena above the first
        public int[] arenaTrophies = { 0, 300, 700, 1200, 1800 };   // keep in sync with MetaRules.Arenas

        // New player
        public int startGold = 100, startGems = 50;

        // Match rewards
        public int winTrophies = 30, lossTrophies = 25;
        public int winGold = 50, lossGold = 15, drawGold = 25;
        /// <summary>Win streak: +percent of winGold per extra win in a row (2nd win +20%, 3rd +40% ... capped).</summary>
        public int streakBonusPercent = 20, streakBonusMaxPercent = 100;
        /// <summary>Winning a revenge rematch: +percent of winGold.</summary>
        public int revengeBonusPercent = 50;
        public int winPassXp = 40, lossPassXp = 20;

        // Shop
        public int dailyDeals = 3;
        public int[] dealShards = { 10, 4, 1, 0 };        // per rarity (legendary not sold for gold)
        public int[] dealGold = { 100, 200, 500, 0 };
        public int freeDailyGold = 50;
        public int adGems = 10;
        public int goldPackSmallGold = 1000, goldPackSmallGems = 60;
        public int goldPackBigGold = 5000, goldPackBigGems = 250;
        public List<GemPack> gemPacks = new List<GemPack>();

        // Gacha
        public int gachaPrice = 100, gachaPrice10 = 900;
        public int[] gachaRarityWeights = { 55, 30, 12, 3 };   // percent
        public int[] gachaShards = { 15, 6, 2, 1 };
        public int gachaPity = 40;                            // legendary guaranteed by this pull

        // Missions
        public int missionsPerDay = 3;
        public List<MissionDef> missionPool = new List<MissionDef>();

        // Season pass
        public long seasonEpochUnix = 1767225600;   // 2026-01-01 00:00 UTC
        public int seasonDays = 28;
        public int passXpPerTier = 100;
        public int passPremiumGems = 600;
        public List<PassTier> passTiers = new List<PassTier>();

        public int UpgradeShards(Rarity rarity, int fromLevel)
        {
            int baseShards = upgradeShards[Math.Min(upgradeShards.Length - 1, fromLevel - 1)];
            int div = rarityShardDivisor[(int)rarity];
            return Math.Max(1, (baseShards + div - 1) / div);
        }

        public int UpgradeGold(int fromLevel) { return upgradeGold[Math.Min(upgradeGold.Length - 1, fromLevel - 1)]; }

        public ChestDef Chest(string id)
        {
            for (int i = 0; i < chests.Count; i++) if (chests[i].id == id) return chests[i];
            return null;
        }

        public MissionDef Mission(string id)
        {
            for (int i = 0; i < missionPool.Count; i++) if (missionPool[i].id == id) return missionPool[i];
            return null;
        }
    }

    public static class DefaultMeta
    {
        public static MetaConfig Create()
        {
            var c = new MetaConfig();
            c.chests.Add(new ChestDef { id = "wood", unlockSeconds = 3 * 3600, goldMin = 40, goldMax = 60, shards = 12, epicChance = 5, legendaryChance = 0, gemPrice = 0 });
            c.chests.Add(new ChestDef { id = "silver", unlockSeconds = 8 * 3600, goldMin = 100, goldMax = 140, shards = 30, epicChance = 15, legendaryChance = 1, gemPrice = 60 });
            c.chests.Add(new ChestDef { id = "gold", unlockSeconds = 12 * 3600, goldMin = 250, goldMax = 350, shards = 70, epicChance = 40, legendaryChance = 4, gemPrice = 150 });
            c.chests.Add(new ChestDef { id = "magic", unlockSeconds = 24 * 3600, goldMin = 600, goldMax = 800, shards = 160, epicChance = 100, legendaryChance = 15, gemPrice = 400 });

            c.gemPacks.Add(new GemPack { id = "gems_small", gems = 80, price = "$0.99" });
            c.gemPacks.Add(new GemPack { id = "gems_medium", gems = 500, price = "$4.99" });
            c.gemPacks.Add(new GemPack { id = "gems_large", gems = 1200, price = "$9.99" });

            c.missionPool.Add(new MissionDef { id = "play3", type = MissionType.PlayMatches, target = 3, gold = 60, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "win2", type = MissionType.WinMatches, target = 2, gold = 100, passXp = 60 });
            c.missionPool.Add(new MissionDef { id = "kill400", type = MissionType.KillEnemies, target = 400, gold = 80, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "merge25", type = MissionType.MergeUnits, target = 25, gold = 80, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "send12", type = MissionType.SendMonsters, target = 12, gold = 80, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "wave12", type = MissionType.ReachWave, target = 12, gold = 100, passXp = 60 });
            c.missionPool.Add(new MissionDef { id = "buy40", type = MissionType.BuyCards, target = 40, gold = 60, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "chest1", type = MissionType.OpenChests, target = 1, gold = 60, passXp = 40 });
            c.missionPool.Add(new MissionDef { id = "upgrade1", type = MissionType.UpgradeUnits, target = 1, gold = 60, passXp = 40 });
            c.missionPool.Add(new MissionDef { id = "hits150", type = MissionType.EffectiveHits, target = 150, gold = 80, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "boss2", type = MissionType.KillBosses, target = 2, gold = 80, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "modes2", type = MissionType.PlayModes, target = 2, gold = 70, passXp = 50 });
            c.missionPool.Add(new MissionDef { id = "daily1", type = MissionType.DailyChallenge, target = 1, gold = 60, passXp = 40 });

            // 30 tiers: free track = gold and chests, premium = gems, bigger chests and a legendary shard.
            for (int t = 1; t <= 30; t++)
            {
                var tier = new PassTier();
                if (t % 15 == 0) tier.free = new Reward(RewardType.Chest, 1, "gold");
                else if (t % 5 == 0) tier.free = new Reward(RewardType.Chest, 1, "silver");
                else tier.free = new Reward(RewardType.Gold, 60 + 10 * t);

                if (t == 30) tier.premium = new Reward(RewardType.Chest, 1, "magic");
                else if (t == 20) tier.premium = new Reward(RewardType.Chest, 1, "magic");
                else if (t % 3 == 0) tier.premium = new Reward(RewardType.Gems, 20);
                else if (t % 5 == 0) tier.premium = new Reward(RewardType.Chest, 1, "gold");
                else tier.premium = new Reward(RewardType.Gold, 150 + 20 * t);
                c.passTiers.Add(tier);
            }
            return c;
        }
    }
}
