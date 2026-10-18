using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Game modes (solo, co-op raid, campaign, mayhem) and the phase 1-3 meta (records, achievements, seasons).</summary>
    public class ModesTests
    {
        static Match RunBots(MatchSetup setup, int maxTicks = 20 * 60 * 30)
        {
            var c = DefaultContent.CreateDatabase();
            var m = new Match(c, setup);
            var bots = new[] { new BotBrain(0, 2, 11), new BotBrain(1, 2, 12) };
            var cmds = new List<Command>();
            while (!m.IsOver && m.Tick < maxTicks)
            {
                cmds.Clear();
                bots[0].Think(m, cmds);
                if (!m.IsSolo) bots[1].Think(m, cmds);
                foreach (var x in cmds) m.Submit(x);
                m.Step();
            }
            return m;
        }

        static MatchSetup Setup(MatchRules rules, int seed = 5)
        {
            return new MatchSetup
            {
                seed = seed, rules = rules,
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            };
        }

        [Test]
        public void Solo_OnlyBoardZeroPlays_AndReplaysExactly()
        {
            var m = RunBots(Setup(MatchRules.Endless()));
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(1, m.Winner, "a solo run ends when the hearts run out");
            Assert.AreEqual(0, m.Boards[1].Enemies.Count, "the idle board never gets monsters");
            var record = MatchRecord.From(m);
            Assert.IsTrue(ReplayValidator.Validate(DefaultContent.CreateDatabase(), record).valid, "mode rules replay");
        }

        [Test]
        public void Solo_RejectsSends()
        {
            var c = DefaultContent.CreateDatabase();
            var m = new Match(c, Setup(MatchRules.Daily()));
            m.Submit(new Command(0, 0, CommandType.SendMonster, 0));
            m.Step();
            bool rejected = false;
            foreach (var e in m.Events) if (e.type == SimEventType.CommandRejected) rejected = true;
            Assert.IsTrue(rejected);
        }

        [Test]
        public void Rules_DoNotChangeTheSharedBalance()
        {
            var c = DefaultContent.CreateDatabase();
            int before = c.Balance.elementBonusPercent;
            var m = new Match(c, Setup(MatchRules.MayhemOf(MayhemRule.ElementChaos)));
            Assert.AreEqual(100, m.Balance.elementBonusPercent);
            Assert.AreEqual(before, c.Balance.elementBonusPercent);
        }

        [Test]
        public void Campaign_WinsAfterTheGoalWave()
        {
            var lv = Campaign.Get(0);
            var m = RunBots(Setup(Campaign.Rules(lv)));
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(0, m.Winner, "the first level is easy for a good bot");
            Assert.AreEqual(lv.winWave, m.Wave);
            Assert.Greater(Campaign.Stars(lv, true, m.Boards[0].Hearts), 0);
        }

        [Test]
        public void Raid_SharesOneBossHpPool()
        {
            var rules = MatchRules.Raid();
            rules.raidBossWave = 1;
            var c = DefaultContent.CreateDatabase();
            var m = new Match(c, Setup(rules));
            while (m.RaidMaxHp == 0) m.Step();
            int hp = m.RaidHp;
            EnemyInstance a = null, b = null;
            foreach (var e in m.Boards[0].Enemies) if (e.raid) a = e;
            foreach (var e in m.Boards[1].Enemies) if (e.raid) b = e;
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(hp, a.hp);
            Assert.AreEqual(hp, b.hp);
            // Assist: candy gift goes to the partner.
            int partner = m.Boards[1].Candy;
            m.Submit(new Command(m.Tick, 0, CommandType.Assist, 0));
            m.Step();
            Assert.Greater(m.Boards[1].Candy, partner);
        }

        [Test]
        public void Daily_IsTheSameForEveryone()
        {
            var c = DefaultContent.CreateDatabase();
            CollectionAssert.AreEqual(DailyChallenge.Deck(c, 20400), DailyChallenge.Deck(c, 20400));
            Assert.AreEqual(5, DailyChallenge.Deck(c, 20400).Length);
        }

        static MetaService Meta()
        {
            var c = DefaultContent.CreateDatabase();
            var cfg = DefaultMeta.Create();
            return new MetaService(cfg, c, MetaService.NewPlayer(cfg, c, 7));
        }

        const long Now = 1790000000;

        [Test]
        public void ApplyMode_TracksRecordsStarsAndMilestones()
        {
            var meta = Meta();
            int gold = meta.State.gold;
            var r = meta.ApplyMode(new MatchSummary { mode = MatchMode.Daily, winner = 1, wave = 11, kills = 50 }, Now);
            Assert.IsTrue(r.newRecord);
            Assert.AreEqual(11, meta.State.dailyBest);
            Assert.Greater(meta.State.gold, gold, "milestones at waves 5 and 10");
            var again = meta.ApplyMode(new MatchSummary { mode = MatchMode.Daily, winner = 1, wave = 11 }, Now);
            Assert.IsFalse(again.newRecord);

            var c = meta.ApplyMode(new MatchSummary { mode = MatchMode.Campaign, winner = 0, level = 0, heartsLeft = 10 }, Now);
            Assert.AreEqual(3, c.stars);
            Assert.IsTrue(meta.IsCampaignOpen(1));
        }

        [Test]
        public void Achievements_ClaimTiersAndUnlockTitles()
        {
            var meta = Meta();
            meta.State.statWins = 200;
            var def = MetaContent.Achievement("winner");
            int gems = meta.State.gems;
            for (int i = 0; i < 3; i++) Assert.IsNotNull(meta.ClaimAchievement("winner"));
            Assert.IsNull(meta.ClaimAchievement("winner"));
            Assert.Greater(meta.State.gems, gems);
            Assert.IsTrue(meta.SetTitle(def.title));
            Assert.IsFalse(meta.SetTitle("not_owned"));
        }

        [Test]
        public void Season_EndRewardsAndSoftResetsTrophies()
        {
            var meta = Meta();
            meta.Refresh(Now);
            meta.State.trophies = 2000;
            meta.State.seasonBestTrophies = 2000;
            meta.Refresh(Now + 29L * 86400);
            Assert.AreEqual(MetaService.SeasonResetFloor + 400, meta.State.trophies);
            Assert.AreEqual(5, meta.State.pendingSeasonTier);
            Assert.IsNotNull(meta.ClaimSeasonReward());
            Assert.AreEqual(0, meta.State.pendingSeasonTier);
        }

        [Test]
        public void Tournament_ThreeWinsMakeAChampion()
        {
            var meta = Meta();
            meta.Refresh(Now);
            Assert.IsTrue(meta.EnterTournament());
            ModeRewards r = null;
            for (int i = 0; i < TournamentRules.Rounds; i++) r = meta.ApplyMode(new MatchSummary { mode = MatchMode.Tournament, winner = 0 }, Now);
            Assert.IsTrue(r.tournamentWon);
            Assert.AreEqual(1, meta.State.statTournamentWins);
            Assert.IsFalse(meta.TournamentRunActive);
        }

        [Test]
        public void Bestiary_FirstLookGivesGemsOnce()
        {
            var meta = Meta();
            meta.Discover(new[] { "fire_fox" }, new[] { "slime" });
            Assert.IsNotNull(meta.ClaimBestiary("slime"));
            Assert.IsNull(meta.ClaimBestiary("slime"));
            Assert.IsNull(meta.ClaimBestiary("imp"), "not met yet");
        }
    }
}
