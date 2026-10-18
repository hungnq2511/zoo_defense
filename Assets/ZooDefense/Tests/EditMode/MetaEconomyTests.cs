using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Out-of-match economy: chests, upgrades, missions, pass, shop, gacha.</summary>
    public class MetaEconomyTests
    {
        const long Day0 = 1767225600 + 86400 * 3;   // a few days into season 1

        static MetaService NewService(out MetaConfig cfg)
        {
            cfg = DefaultMeta.Create();
            var content = DefaultContent.CreateDatabase();
            var state = MetaService.NewPlayer(cfg, content, 7);
            var meta = new MetaService(cfg, content, state);
            meta.Refresh(Day0);
            return meta;
        }

        static MatchSummary Win() { return new MatchSummary { winner = 0, ranked = true, kills = 100, merges = 10, sends = 3, wave = 12, cardsBought = 20 }; }

        [Test]
        public void NewPlayer_HasStarterDeckUnlocked()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            foreach (var id in DefaultContent.DefaultDeck) Assert.IsTrue(meta.IsUnlocked(id), id);
            Assert.AreEqual(cfg.startGold, meta.State.gold);
            Assert.AreEqual(cfg.chestSlots, meta.State.chests.Length);
        }

        [Test]
        public void RankedWin_GivesTrophiesGoldAndChest()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            var r = meta.ApplyMatch(Win(), Day0);
            Assert.AreEqual(cfg.winTrophies, r.trophies);
            Assert.AreEqual(cfg.startGold + cfg.winGold, meta.State.gold);
            Assert.IsTrue(r.chestId != null, "chest");
            Assert.AreEqual(ChestStatus.Locked, meta.Status(0, Day0));
        }

        [Test]
        public void WinStreak_AndRevenge_GiveBonusGold()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            Assert.AreEqual(0, meta.ApplyMatch(Win(), Day0).bonusGold, "first win: no streak yet");
            var second = meta.ApplyMatch(Win(), Day0);
            Assert.AreEqual(2, second.winStreak);
            Assert.AreEqual(cfg.winGold * cfg.streakBonusPercent / 100, second.bonusGold);
            var loss = Win(); loss.winner = 1;
            meta.ApplyMatch(loss, Day0);
            Assert.AreEqual(0, meta.State.winStreak, "a loss resets the streak");
            var revenge = Win(); revenge.revenge = true;
            Assert.AreEqual(cfg.winGold * cfg.revengeBonusPercent / 100, meta.ApplyMatch(revenge, Day0).bonusGold);
        }

        [Test]
        public void ChestSlots_FillUpThenReportFull()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            for (int i = 0; i < cfg.chestSlots; i++) Assert.IsTrue(meta.ApplyMatch(Win(), Day0).chestId != null);
            var r = meta.ApplyMatch(Win(), Day0);
            Assert.IsTrue(r.chestId == null && r.chestSlotsFull, "full");
        }

        [Test]
        public void Chest_UnlocksOverTimeOrWithGems()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            meta.ApplyMatch(Win(), Day0);
            meta.ApplyMatch(Win(), Day0);
            Assert.IsTrue(meta.StartUnlock(0, Day0));
            Assert.IsFalse(meta.StartUnlock(1, Day0), "only one at a time");
            Assert.IsTrue(meta.OpenChest(0, Day0, false) == null, "not ready yet");
            long later = Day0 + cfg.Chest(meta.State.chests[0].chestId).unlockSeconds;
            Assert.AreEqual(ChestStatus.Ready, meta.Status(0, later));
            Assert.IsTrue(meta.OpenChest(0, later, false).Count > 0);
            Assert.AreEqual(ChestStatus.Empty, meta.Status(0, later));

            meta.State.gems = 1000;
            int cost = meta.SkipCost(1, later);
            Assert.IsTrue(cost > 0);
            Assert.IsTrue(meta.OpenChest(1, later, true) != null);
            Assert.AreEqual(1000 - cost, meta.State.gems);
        }

        [Test]
        public void Upgrade_SpendsShardsAndGold()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            string id = DefaultContent.DefaultDeck[0];
            int shards, gold;
            Assert.IsTrue(meta.UpgradeCost(id, out shards, out gold));
            meta.Card(id).shards = shards;
            meta.State.gold = gold;
            Assert.IsTrue(meta.Upgrade(id));
            Assert.AreEqual(2, meta.Level(id));
            Assert.AreEqual(0, meta.State.gold);
            Assert.IsFalse(meta.Upgrade(id), "no more shards");
        }

        [Test]
        public void Gacha_RespectsPity()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            meta.State.gems = cfg.gachaPrice10 * 10;
            meta.State.gachaPity = cfg.gachaPity - 1;
            var rewards = meta.Pull(1);
            var def = DefaultContent.CreateDatabase().Unit(rewards[0].id);
            Assert.AreEqual((int)Rarity.Legendary, (int)def.rarity);
            Assert.AreEqual(0, meta.State.gachaPity);
            Assert.IsTrue(meta.Pull(10).Count == 10);
        }

        [Test]
        public void Missions_ProgressAndClaimOnce()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            Assert.AreEqual(cfg.missionsPerDay, meta.State.missions.Count);
            for (int i = 0; i < 5; i++) meta.ApplyMatch(Win(), Day0);
            meta.Upgrade(DefaultContent.DefaultDeck[0]);
            meta.State.gems = 1000;
            meta.OpenChest(0, Day0, true);
            int claimable = meta.ClaimableMissions();
            Assert.IsTrue(claimable > 0, "some mission done");
            for (int i = 0; i < meta.State.missions.Count; i++) meta.ClaimMission(i);
            Assert.AreEqual(0, meta.ClaimableMissions());
            Assert.IsTrue(meta.ClaimMission(0) == null, "claim once");
            meta.Refresh(Day0 + 86400);
            Assert.IsFalse(meta.State.missions[0].claimed, "new day, new missions");
        }

        [Test]
        public void Pass_FreeAndPremiumTracks()
        {
            MetaConfig cfg;
            var meta = NewService(out cfg);
            meta.State.passXp = cfg.passXpPerTier * 2;
            Assert.AreEqual(2, meta.PassTier);
            Assert.IsTrue(meta.ClaimPass(1, false) != null);
            Assert.IsTrue(meta.ClaimPass(1, false) == null, "once");
            Assert.IsTrue(meta.ClaimPass(1, true) == null, "premium locked");
            Assert.IsFalse(meta.CanClaimPass(3, false), "tier not reached");
            meta.State.gems = cfg.passPremiumGems;
            Assert.IsTrue(meta.BuyPremiumPass());
            Assert.IsTrue(meta.ClaimPass(1, true) != null);
        }

        [Test]
        public void CardLevels_RaiseDamage()
        {
            var content = DefaultContent.CreateDatabase();
            var setup = new MatchSetup
            {
                seed = 3,
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck, cardLevels = new[] { 5, 1, 1, 1, 1 } },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            };
            var m = new Match(content, setup);
            int unit = content.UnitIndex(DefaultContent.DefaultDeck[0]);
            Assert.AreEqual(100 + 4 * content.Balance.cardLevelDamagePercent, m.Boards[0].CardPercent(unit));
            Assert.AreEqual(100, m.Boards[1].CardPercent(unit));
        }
    }
}
