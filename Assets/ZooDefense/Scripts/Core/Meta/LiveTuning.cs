using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// Values the team can change from the Unity dashboard (Remote Config) without a new app version.
    /// Every field has a safe default, so the game runs the same offline or before the first fetch.
    /// Remote Config keys (type in the dashboard):
    ///   bot_wait_seconds (float), reconnect_seconds (float), ad_first_after (int), ad_every (int),
    ///   ad_min_seconds (float), notify_per_day (int), live_event (int, -1 = calendar),
    ///   meta_overrides (json: MetaConfig fields), balance_overrides (json: full balance export).
    /// </summary>
    [Serializable]
    public sealed class LiveTuning
    {
        /// <summary>Ranked search: seconds before a bot match is offered instead of a real player.</summary>
        public const float MaxBotWaitSeconds = 6f;
        public float botWaitSeconds = 6f;
        /// <summary>Online match: how long a dropped player has to come back before the match is lost.</summary>
        public float reconnectSeconds = 20f;
        /// <summary>Full-screen ads: first after this many matches, then every N matches, at least this many seconds apart.</summary>
        public int adFirstAfter = 3;
        public int adEvery = 2;
        public float adMinSeconds = 240f;
        /// <summary>Local notifications per day at most.</summary>
        public int notifyPerDay = 2;
        /// <summary>Force a week's live event (index of the week), -1 = follow the calendar.</summary>
        public int liveEvent = -1;
        /// <summary>JSON with MetaConfig fields to override (prices, rewards). Applied at the next app start.</summary>
        public string metaOverrides = "";
        /// <summary>JSON of a full balance export (animals, monsters). Applied at the next app start.</summary>
        public string balanceOverrides = "";

        public static LiveTuning FromConfig(Dictionary<string, object> cfg)
        {
            var t = new LiveTuning();
            if (cfg == null) return t;
            // Capped at 6 s (Oct 2026): an old Remote Config value (10 s) must not bring back the long wait.
            t.botWaitSeconds = Clamp((float)MiniJson.Num(cfg, "bot_wait_seconds", t.botWaitSeconds), 3f, MaxBotWaitSeconds);
            t.reconnectSeconds = Clamp((float)MiniJson.Num(cfg, "reconnect_seconds", t.reconnectSeconds), 5f, 60f);
            t.adFirstAfter = Math.Max(0, Math.Min(50, MiniJson.Int(cfg, "ad_first_after", t.adFirstAfter)));
            t.adEvery = Math.Max(1, Math.Min(50, MiniJson.Int(cfg, "ad_every", t.adEvery)));
            t.adMinSeconds = Clamp((float)MiniJson.Num(cfg, "ad_min_seconds", t.adMinSeconds), 30f, 3600f);
            t.notifyPerDay = Math.Max(0, Math.Min(4, MiniJson.Int(cfg, "notify_per_day", t.notifyPerDay)));
            t.liveEvent = Math.Max(-1, MiniJson.Int(cfg, "live_event", t.liveEvent));
            t.metaOverrides = JsonText(cfg, "meta_overrides");
            t.balanceOverrides = JsonText(cfg, "balance_overrides");
            return t;
        }

        /// <summary>A JSON value may arrive as a string (Remote Config "json" type is sometimes sent as text) or as an object.</summary>
        static string JsonText(Dictionary<string, object> cfg, string key)
        {
            object v;
            if (!cfg.TryGetValue(key, out v) || v == null) return "";
            if (v is string) return ((string)v).Trim();
            if (v is Dictionary<string, object>) return MiniJson.Serialize(v);
            return "";
        }

        static float Clamp(float v, float min, float max) { return float.IsNaN(v) ? min : Math.Max(min, Math.Min(max, v)); }
    }

    // ===================================================================== local notifications plan

    public enum NotifyKind { ChestReady = 1, LoginReward = 2, FreeGift = 3, NewMissions = 4, MonthlyCard = 5, ComeBack = 6 }

    public struct PlannedNotification
    {
        public NotifyKind kind;
        /// <summary>Unix seconds.</summary>
        public long at;
        /// <summary>Text argument (chest id, login day number...).</summary>
        public string arg;
        public int Id { get { return 1000 + (int)kind; } }
    }

    /// <summary>
    /// Decides which local notifications to schedule when the player leaves the game. Pure function of the economy
    /// state, so it is tested without a phone. At most `perDay` per local day, never at night (moved to 09:00).
    /// </summary>
    public static class NotificationPlanner
    {
        public const int QuietFrom = 22, QuietTo = 9;   // local hours
        const int Hour = 3600;

        /// <param name="utcOffsetSeconds">The phone's local time offset from UTC.</param>
        public static List<PlannedNotification> Plan(MetaService meta, long now, int utcOffsetSeconds, int perDay, int horizonDays = 3)
        {
            var list = new List<PlannedNotification>();
            if (perDay <= 0) return list;
            var s = meta.State;

            // A chest finishes unlocking.
            long chestAt = long.MaxValue; string chestId = null;
            for (int i = 0; i < s.chests.Length; i++)
            {
                if (meta.Status(i, now) != ChestStatus.Unlocking) continue;
                long end = s.chests[i].unlockEndsAt;
                if (end > now + 60 && end < chestAt) { chestAt = end; chestId = s.chests[i].chestId; }
            }
            if (chestId != null) list.Add(new PlannedNotification { kind = NotifyKind.ChestReady, at = chestAt, arg = chestId });

            // The next game day (UTC reset): login reward, free gift and new missions. One message, at local lunchtime.
            long nextDay = (MetaService.Day(now) + 1) * (long)MetaService.SecondsPerDay;
            long dayMsg = LocalTimeOnOrAfter(nextDay, 12, utcOffsetSeconds);
            // Day number of the reward waiting tomorrow (an unclaimed reward stays the same day; a finished cycle restarts).
            int nextLoginDay = (s.loginStep >= meta.LoginDays ? 0 : s.loginStep) + 1;
            list.Add(new PlannedNotification { kind = NotifyKind.LoginReward, at = dayMsg, arg = nextLoginDay.ToString() });

            // Monthly card: today's gems not taken yet (evening reminder), or tomorrow's.
            if (meta.MonthlyActive(now))
            {
                long evening = LocalTimeOnOrAfter(now, 19, utcOffsetSeconds);
                long when = meta.CanClaimMonthly(now) && evening > now + Hour && MetaService.Day(evening) == MetaService.Day(now)
                    ? evening : LocalTimeOnOrAfter(nextDay, 19, utcOffsetSeconds);
                if (meta.State.monthlyUntilDay >= MetaService.Day(when)) list.Add(new PlannedNotification { kind = NotifyKind.MonthlyCard, at = when });
            }

            // Away for two days: a friendly come-back.
            list.Add(new PlannedNotification { kind = NotifyKind.ComeBack, at = LocalTimeOnOrAfter(now + 2 * 86400L, 18, utcOffsetSeconds) });

            // Night -> 09:00, sort, at most perDay per local day, within the horizon.
            for (int i = 0; i < list.Count; i++) { var n = list[i]; n.at = OutOfQuietHours(n.at, utcOffsetSeconds); list[i] = n; }
            list.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : ((int)a.kind).CompareTo((int)b.kind));
            var result = new List<PlannedNotification>();
            var perLocalDay = new Dictionary<long, int>();
            foreach (var n in list)
            {
                if (n.at <= now || n.at > now + horizonDays * 86400L) continue;
                long localDay = (n.at + utcOffsetSeconds) / 86400;
                int count;
                perLocalDay.TryGetValue(localDay, out count);
                if (count >= perDay) continue;
                perLocalDay[localDay] = count + 1;
                result.Add(n);
            }
            return result;
        }

        /// <summary>The first moment at or after `t` whose local hour is `hour`:00.</summary>
        public static long LocalTimeOnOrAfter(long t, int hour, int utcOffsetSeconds)
        {
            long local = t + utcOffsetSeconds;
            long dayStart = local - Mod(local, 86400);
            long candidate = dayStart + hour * (long)Hour;
            if (candidate < local) candidate += 86400;
            return candidate - utcOffsetSeconds;
        }

        /// <summary>Moves a time inside 22:00-09:00 local to the next 09:00.</summary>
        public static long OutOfQuietHours(long t, int utcOffsetSeconds)
        {
            long local = t + utcOffsetSeconds;
            int hour = (int)(Mod(local, 86400) / Hour);
            if (hour >= QuietFrom || hour < QuietTo) return LocalTimeOnOrAfter(t, QuietTo, utcOffsetSeconds);
            return t;
        }

        static long Mod(long a, long m) { long r = a % m; return r < 0 ? r + m : r; }
    }
}
