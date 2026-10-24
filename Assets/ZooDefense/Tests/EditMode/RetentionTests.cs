using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Login calendar, hidden rating, notification plan, Remote Config values and the JSON reader.</summary>
    public class RetentionTests
    {
        const long Day = 86400;
        const long Monday = 1767571200;   // 2026-01-05 00:00 UTC

        static MetaService NewMeta(out PlayerState s)
        {
            var cfg = DefaultMeta.Create();
            var content = DefaultContent.CreateDatabase();
            s = MetaService.NewPlayer(cfg, content, 42);
            return new MetaService(cfg, content, s);
        }

        [Test]
        public void Login_OneRewardPerDay_SevenDayCycle()
        {
            PlayerState s; var m = NewMeta(out s);
            long t = Monday + 10 * 3600;
            Assert.IsTrue(m.CanClaimLogin(t));
            int gold = s.gold;
            var r = m.ClaimLogin(t);
            Assert.IsNotNull(r);
            Assert.AreEqual(gold + 200, s.gold, "day 1 = 200 gold");
            Assert.IsNull(m.ClaimLogin(t + 3600), "only once a day");
            Assert.AreEqual(LoginDayState.Claimed, m.LoginDay(0, t));
            Assert.AreEqual(LoginDayState.Later, m.LoginDay(1, t));
            Assert.AreEqual(LoginDayState.Today, m.LoginDay(1, t + Day));
            for (int d = 1; d < 7; d++) Assert.IsNotNull(m.ClaimLogin(t + d * Day));
            Assert.AreEqual(7, s.loginStep);
            // Day 8 starts a new cycle with the day-1 reward.
            Assert.AreEqual(LoginDayState.Today, m.LoginDay(0, t + 7 * Day));
            gold = s.gold;
            m.ClaimLogin(t + 7 * Day);
            Assert.AreEqual(1, s.loginStep);
            Assert.AreEqual(1, s.loginCycles);
            Assert.AreEqual(gold + 200, s.gold);
        }

        [Test]
        public void Login_MissedDay_DoesNotResetTheCycle()
        {
            PlayerState s; var m = NewMeta(out s);
            long t = Monday + 3600;
            m.ClaimLogin(t);
            m.ClaimLogin(t + Day);
            // Three days away: the next claim is day 3, not day 1.
            Assert.AreEqual(2, m.LoginTodayIndex(t + 4 * Day));
            m.ClaimLogin(t + 4 * Day);
            Assert.AreEqual(3, s.loginStep);
        }

        [Test]
        public void Rating_WinsRaiseLossesLower_AndRankedOnly()
        {
            PlayerState s; var m = NewMeta(out s);
            Assert.AreEqual(MatchmakingRules.StartMmr, s.mmr, "new player starts at 1000");
            int start = s.mmr;
            var win = new MatchSummary { winner = 0, ranked = true, opponentMmr = 1000 };
            var r = m.ApplyMatch(win, Monday);
            Assert.Greater(r.mmrDelta, 0);
            Assert.AreEqual(start + r.mmrDelta, s.mmr);
            int afterWin = s.mmr;
            m.ApplyMatch(new MatchSummary { winner = 1, ranked = false, opponentMmr = 1000 }, Monday);
            Assert.AreEqual(afterWin, s.mmr, "practice / friendly never changes the rating");
            m.ApplyMatch(new MatchSummary { winner = 1, ranked = true, opponentMmr = 1000 }, Monday);
            Assert.Less(s.mmr, afterWin);
            // Beating a much stronger player is worth more than beating a weaker one.
            Assert.Greater(MatchmakingRules.EloDelta(1000, 1400, 0, 100), MatchmakingRules.EloDelta(1000, 600, 0, 100));
        }

        [Test]
        public void Rating_BotsFitTheRating()
        {
            Assert.AreEqual(0, MatchmakingRules.BotDifficulty(800));
            Assert.AreEqual(1, MatchmakingRules.BotDifficulty(1000));
            Assert.AreEqual(2, MatchmakingRules.BotDifficulty(1600));
            Assert.AreEqual(-1, MatchmakingRules.BotLevelOffset(700));
            int bot = MatchmakingRules.BotMmr(1000, 12345);
            Assert.IsTrue(bot >= 940 && bot <= 1060);
            Assert.AreEqual(150, MatchmakingRules.Window(0));
            Assert.AreEqual(MatchmakingRules.Windows[MatchmakingRules.Windows.Length - 1], MatchmakingRules.Window(99));
        }

        [Test]
        public void OldSave_GetsARatingFromTrophies()
        {
            var cfg = DefaultMeta.Create();
            var content = DefaultContent.CreateDatabase();
            var s = MetaService.NewPlayer(cfg, content, 1);
            s.mmr = 0;
            s.trophies = 800;
            new MetaService(cfg, content, s);
            Assert.AreEqual(1200, s.mmr);
        }

        [Test]
        public void Invite_RewardsOnlyOnce_AndOnlyNewFriends()
        {
            PlayerState s; var m = NewMeta(out s);
            int gems = s.gems;
            Assert.IsNotNull(m.RedeemInvite());
            Assert.IsNull(m.RedeemInvite());
            Assert.AreEqual(gems + 100, s.gems);
            gems = s.gems;
            m.ClaimInviteRewards(2);
            Assert.AreEqual(gems + 200, s.gems);
            Assert.IsNull(m.ClaimInviteRewards(2), "already rewarded");
            m.ClaimInviteRewards(3);
            Assert.AreEqual(gems + 300, s.gems);
        }

        [Test]
        public void Notifications_NeverAtNight_AtMostPerDay()
        {
            PlayerState s; var m = NewMeta(out s);
            long now = Monday + 20 * 3600;   // 20:00 UTC
            int offset = 7 * 3600;           // UTC+7: 03:00 local
            // Start a chest so a chest notification is due in the night.
            s.chests[0].chestId = "wood";
            m.StartUnlock(0, now);
            var plan = NotificationPlanner.Plan(m, now, offset, 2);
            Assert.Greater(plan.Count, 0);
            var perDay = new Dictionary<long, int>();
            foreach (var n in plan)
            {
                long local = n.at + offset;
                int hour = (int)((local % Day) / 3600);
                Assert.IsTrue(hour >= NotificationPlanner.QuietTo && hour < NotificationPlanner.QuietFrom, "not between 22:00 and 09:00: " + hour);
                Assert.IsTrue(n.at > now);
                long d = local / Day;
                int c; perDay.TryGetValue(d, out c); perDay[d] = c + 1;
            }
            foreach (var kv in perDay) Assert.IsTrue(kv.Value <= 2, "at most 2 a day");
            Assert.AreEqual(0, NotificationPlanner.Plan(m, now, offset, 0).Count, "switched off");
            bool chest = false;
            foreach (var n in plan) if (n.kind == NotifyKind.ChestReady) chest = true;
            Assert.IsTrue(chest, "chest ready is planned");
        }

        [Test]
        public void Notifications_LocalTimeHelpers()
        {
            int offset = -5 * 3600;   // UTC-5
            long t = Monday;          // 19:00 local the day before
            long noon = NotificationPlanner.LocalTimeOnOrAfter(t, 12, offset);
            Assert.AreEqual(12, (int)(((noon + offset) % Day + Day) % Day / 3600));
            Assert.IsTrue(noon > t);
            long night = Monday + 5 * 3600;   // 00:00 local
            long moved = NotificationPlanner.OutOfQuietHours(night, offset);
            Assert.AreEqual(9, (int)(((moved + offset) % Day + Day) % Day / 3600));
        }

        [Test]
        public void Tuning_ReadsRemoteValues_WithSafeLimits()
        {
            var cfg = MiniJson.Object("{\"bot_wait_seconds\": 15, \"reconnect_seconds\": \"25\", \"ad_every\": 0, \"live_event\": 3, \"meta_overrides\": {\"winGold\": 60}}");
            var t = LiveTuning.FromConfig(cfg);
            Assert.AreEqual(15f, t.botWaitSeconds);
            Assert.AreEqual(25f, t.reconnectSeconds);
            Assert.AreEqual(1, t.adEvery, "clamped");
            Assert.AreEqual(3, t.liveEvent);
            Assert.AreEqual("{\"winGold\":60}", t.metaOverrides);
            var d = LiveTuning.FromConfig(null);
            Assert.AreEqual(10f, d.botWaitSeconds, "defaults offline");
            Assert.AreEqual(-1, d.liveEvent);
        }

        [Test]
        public void MiniJson_RoundTrip()
        {
            var o = MiniJson.Object("{\"a\": [1, 2.5, \"x\\n\\u0041\"], \"b\": {\"c\": true, \"d\": null}, \"e\": -3e2}");
            Assert.IsNotNull(o);
            var a = MiniJson.Arr(o, "a");
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(2.5, (double)a[1]);
            Assert.AreEqual("x\nA", (string)a[2]);
            Assert.IsTrue(MiniJson.Bool(MiniJson.Obj(o, "b"), "c"));
            Assert.AreEqual(-300, MiniJson.Int(o, "e"));
            Assert.IsNull(MiniJson.Parse("{\"a\": }"), "invalid json -> null");
            var back = MiniJson.Object(MiniJson.Serialize(o));
            Assert.AreEqual("x\nA", (string)MiniJson.Arr(back, "a")[2]);
        }

        [Test]
        public void LiveEvent_CanBeForcedRemotely()
        {
            long now = Monday;
            int week = GameCalendar.WeekIndex(now);
            try
            {
                LiveOps.WeekOverride = week + 3;
                Assert.AreEqual(LiveOps.Of(week + 3).kind, LiveOps.Current(now).kind);
                Assert.AreEqual(week + 3, LiveOps.Current(now).week);
            }
            finally { LiveOps.WeekOverride = -1; }
            Assert.AreEqual(week, LiveOps.Current(now).week);
        }
    }
}
