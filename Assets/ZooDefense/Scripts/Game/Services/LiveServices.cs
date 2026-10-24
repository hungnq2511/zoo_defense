using System;
using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    // Live-game services behind thin interfaces (same idea as Services.cs): the ZooDefense.Online assembly plugs in the
    // Unity Gaming Services versions at start-up; without it every call is a harmless no-op and the game runs offline.

    // ===================================================================== Remote Config

    public interface IRemoteConfigService
    {
        /// <summary>Fetches the "settings" config; `done` gets the key/value map (null = failed, keep the cached values).</summary>
        void Fetch(Action<Dictionary<string, object>> done);
    }

    /// <summary>
    /// Current live tuning (Remote Config). Cached on the phone, so the last known values are used at once on the
    /// next launch and offline. Simple values apply right after a fetch; balance and economy overrides only at the
    /// next app start, so nothing changes in the middle of a session (or of an online match).
    /// </summary>
    public static class Tuning
    {
        const string CacheKey = "zoo_remote_config";
        public static IRemoteConfigService Service;
        public static LiveTuning Current { get; private set; }
        public static event Action Changed;

        static Tuning() { Current = LiveTuning.FromConfig(MiniJson.Object(PlayerPrefs.GetString(CacheKey, ""))); ApplySimple(); }

        /// <summary>Overrides for the economy and the balance (from the cache) before GameApp builds them.</summary>
        public static void ApplyStartupOverrides(MetaConfig meta, ref ContentDatabase content)
        {
            var t = Current;
            if (!string.IsNullOrEmpty(t.metaOverrides))
            {
                try { JsonUtility.FromJsonOverwrite(t.metaOverrides, meta); Debug.Log("[Tuning] economy overrides applied"); }
                catch (Exception e) { Debug.LogWarning("[Tuning] meta_overrides ignored: " + e.Message); }
            }
            if (!string.IsNullOrEmpty(t.balanceOverrides))
            {
                try
                {
                    var data = JsonUtility.FromJson<ContentData>(t.balanceOverrides);
                    if (data != null && data.units != null && data.units.Count > 0 && data.enemies != null && data.enemies.Count > 0)
                    {
                        content = new ContentDatabase(data);
                        Debug.Log("[Tuning] balance overrides applied (" + data.units.Count + " animals)");
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Tuning] balance_overrides ignored: " + e.Message); }
            }
        }

        public static void Refresh()
        {
            if (Service == null) return;
            Service.Fetch(cfg =>
            {
                if (cfg == null) return;
                PlayerPrefs.SetString(CacheKey, MiniJson.Serialize(cfg));
                PlayerPrefs.Save();
                Current = LiveTuning.FromConfig(cfg);
                ApplySimple();
                if (Changed != null) Changed();
            });
        }

        static void ApplySimple()
        {
            LiveOps.WeekOverride = Current.liveEvent;
            AdPolicy.FirstAfterMatches = Current.adFirstAfter;
            AdPolicy.EveryMatches = Current.adEvery;
            AdPolicy.MinSeconds = Current.adMinSeconds;
        }
    }

    // ===================================================================== analytics helpers

    /// <summary>Events the dashboard reports on (funnel, retention, revenue). Names match the Event Manager setup (Docs/LIVE_SERVICES.md).</summary>
    public static class Track
    {
        static void Log(string name, Dictionary<string, object> p = null) { if (Services.Analytics != null) Services.Analytics.LogEvent(name, p); }

        public static void TutorialComplete(bool skipped) { Log("tutorial_complete", new Dictionary<string, object> { { "skipped", skipped } }); }

        public static void MatchStart(MatchMode mode, bool ranked, bool online, bool friendly)
        {
            Log("match_start", new Dictionary<string, object> { { "mode", mode.ToString() }, { "ranked", ranked }, { "online", online }, { "friendly", friendly } });
        }

        public static void MatchEnd(MatchMode mode, bool ranked, bool online, bool friendly, int winner, int wave, int seconds)
        {
            Log("match_end", new Dictionary<string, object>
            {
                { "mode", mode.ToString() }, { "ranked", ranked }, { "online", online }, { "friendly", friendly },
                { "result", winner == 0 ? "win" : (winner == 1 ? "loss" : "draw") }, { "wave", wave }, { "seconds", seconds },
            });
        }

        public static void ChestOpen(string chestId, bool paidGems) { Log("chest_open", new Dictionary<string, object> { { "chest", chestId ?? "" }, { "gems", paidGems } }); }
        public static void ArenaUp(int arena) { Log("arena_up", new Dictionary<string, object> { { "arena", arena } }); }
        public static void LoginReward(int day) { Log("login_reward", new Dictionary<string, object> { { "day", day } }); }
        public static void Reconnect(bool success, int seconds) { Log("reconnect", new Dictionary<string, object> { { "success", success }, { "seconds", seconds } }); }
        public static void Social(string action) { Log("social", new Dictionary<string, object> { { "action", action } }); }
    }

    // ===================================================================== crash / error reports

    /// <summary>
    /// Unity Cloud Diagnostics catches native crashes (enabled in the build). This adds the last steps the player took
    /// and sends script errors as a "client_error" event (at most a few per session), so the dashboard shows where
    /// things go wrong even without a crash.
    /// </summary>
    public static class ErrorReporter
    {
        const int MaxPerSession = 8;
        static readonly Queue<string> _steps = new Queue<string>();
        static readonly HashSet<string> _sent = new HashSet<string>();
        static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>Remembers a step (screen opened, button) for the next error report.</summary>
        public static void Step(string what)
        {
            _steps.Enqueue(what);
            while (_steps.Count > 6) _steps.Dequeue();
        }

        public static string LastSteps { get { return string.Join(" > ", _steps.ToArray()); } }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (_sent.Count >= MaxPerSession || string.IsNullOrEmpty(message)) return;
            string first = message.Length > 160 ? message.Substring(0, 160) : message;
            if (!_sent.Add(first)) return;
            string where = "";
            if (!string.IsNullOrEmpty(stack))
            {
                int nl = stack.IndexOf('\n');
                where = (nl > 0 ? stack.Substring(0, nl) : stack).Trim();
                if (where.Length > 160) where = where.Substring(0, 160);
            }
            if (Services.Analytics == null) return;
            Services.Analytics.LogEvent("client_error", new Dictionary<string, object>
            {
                { "message", first }, { "where", where }, { "steps", LastSteps }, { "exception", type == LogType.Exception },
            });
        }
    }

    // ===================================================================== leaderboards

    public enum LeaderboardKind { Trophies = 0, Endless = 1, Daily = 2 }

    public sealed class LeaderboardEntry
    {
        public string playerId;
        public string name;
        public string title;
        /// <summary>1-based.</summary>
        public int rank;
        public long score;
        public int arena;
        public bool me;
    }

    public interface ILeaderboardService
    {
        bool Available { get; }
        /// <summary>Sends a new best (checked on the server, Cloud Code "submit_score").</summary>
        void Submit(LeaderboardKind kind, long score, Dictionary<string, object> info);
        void Top(LeaderboardKind kind, int limit, Action<List<LeaderboardEntry>> done);
        void AroundMe(LeaderboardKind kind, int range, Action<List<LeaderboardEntry>> done);
        void Players(LeaderboardKind kind, List<string> playerIds, Action<List<LeaderboardEntry>> done);
    }

    public static class Leaderboards
    {
        public static ILeaderboardService Service;

        public static string Id(LeaderboardKind k)
        {
            switch (k) { case LeaderboardKind.Endless: return "endless_weekly"; case LeaderboardKind.Daily: return "daily_challenge"; default: return "trophies"; }
        }

        /// <summary>Called after matches / on the lobby: sends the scores that improved since the last submission.</summary>
        public static void SubmitProgress(GameApp app)
        {
            var st = app.Meta.State;
            var info = new Dictionary<string, object> { { "n", app.Profile.playerName }, { "t", st.title ?? "" }, { "a", app.Meta.ArenaIndex() } };
            SubmitIfChanged(LeaderboardKind.Trophies, st.trophies, info, app.Now);
            if (st.endlessBest > 0) SubmitIfChanged(LeaderboardKind.Endless, st.endlessBest, info, app.Now);
            if (st.dailyBest > 0) SubmitIfChanged(LeaderboardKind.Daily, st.dailyBest, info, app.Now);
            GameCenter.Report(st.trophies, st.endlessBest, st.dailyBest);
        }

        static void SubmitIfChanged(LeaderboardKind kind, long score, Dictionary<string, object> info, long now)
        {
            // One key per board and period (daily / weekly boards reset on the server, so resend after a reset).
            string period = kind == LeaderboardKind.Daily ? GameCalendar.DayIndex(now).ToString() : (kind == LeaderboardKind.Endless ? GameCalendar.WeekIndex(now).ToString() : "");
            string key = "zoo_lb_" + Id(kind);
            string value = period + ":" + score;
            if (PlayerPrefs.GetString(key, "") == value) return;
            if (Service == null || !Service.Available) return;
            PlayerPrefs.SetString(key, value);
            Service.Submit(kind, score, info);
        }
    }

    /// <summary>Apple Game Center: sign-in, leaderboards in the Games app, achievements (iPhone only).</summary>
    public static class GameCenter
    {
        // Leaderboard ids created in App Store Connect (Features > Game Center), same names as the Unity boards.
        public const string TrophiesBoard = "zoo.trophies", EndlessBoard = "zoo.endless_weekly", DailyBoard = "zoo.daily_challenge";
        static bool _started;

        public static bool SignedIn
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Social.localUser != null && Social.localUser.authenticated;
#else
                return false;
#endif
            }
        }

        public static void SignIn()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_started) return;
            _started = true;
            Social.localUser.Authenticate(ok => Debug.Log("[GameCenter] signed in: " + ok));
#endif
        }

        public static void Report(int trophies, int endlessBest, int dailyBest)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!SignedIn) return;
            Social.ReportScore(trophies, TrophiesBoard, ok => { });
            if (endlessBest > 0) Social.ReportScore(endlessBest, EndlessBoard, ok => { });
            if (dailyBest > 0) Social.ReportScore(dailyBest, DailyBoard, ok => { });
#endif
        }

        public static void ShowLeaderboards()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (SignedIn) Social.ShowLeaderboardUI();
            else SignIn();
#endif
        }

        public static bool Supported
        {
            get
            {
#if UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }
    }

    // ===================================================================== friends / invites

    /// <summary>What other players can see about us (Cloud Save public data), refreshed while the game is open.</summary>
    [Serializable]
    public class PublicProfile
    {
        public string name = "";
        public string title = "";
        public int trophies;
        public int arena = 1;
        public string lead = "";
        /// <summary>Unix seconds of the last update: "online" when recent.</summary>
        public long seen;
        /// <summary>Friendly-room invite: room code, the invited player's id, when.</summary>
        public string room = "";
        public string roomFor = "";
        public long roomAt;
    }

    public sealed class FriendInfo
    {
        public string playerId;
        public PublicProfile profile;
        public bool Online(long now) { return profile != null && now - profile.seen < 150; }
        /// <summary>This friend invited us to their room in the last few minutes.</summary>
        public bool InvitesMe(string myId, long now)
        {
            return profile != null && !string.IsNullOrEmpty(profile.room) && profile.roomFor == myId && now - profile.roomAt < 300;
        }
    }

    public interface ISocialService
    {
        bool Available { get; }
        string PlayerId { get; }
        /// <summary>Our 6-letter friend / invite code (created on the server the first time).</summary>
        void MyCode(Action<string> done);
        /// <summary>Finds the player behind a code (null + error key when unknown).</summary>
        void Resolve(string code, Action<string, string> done);
        void LoadProfiles(List<string> playerIds, Action<List<FriendInfo>> done);
        void Publish(PublicProfile me);
        /// <summary>New player: enter a friend's code (both get gems). done(ok, errorKey).</summary>
        void RedeemInvite(string code, Action<bool, string> done);
        /// <summary>Number of players who joined with our code (server count).</summary>
        void InviteCount(Action<int> done);
    }

    public static class SocialHub
    {
        public static ISocialService Service;
        public static bool Available { get { return Service != null && Service.Available; } }
        public const string StoreLink = "https://apps.apple.com/app/id6824369094";
        public const string PlayLink = "https://play.google.com/store/apps/details?id=com.hungnq.zoodefense";

        public static string ShareLink { get { return Application.platform == RuntimePlatform.Android ? PlayLink : StoreLink; } }

        public static PublicProfile Me(GameApp app, string room = "", string roomFor = "")
        {
            var st = app.Meta.State;
            return new PublicProfile
            {
                name = app.Profile.playerName, title = st.title ?? "", trophies = st.trophies, arena = app.Meta.ArenaIndex(),
                lead = app.Profile.deck != null && app.Profile.deck.Length > 0 ? app.Profile.deck[0] : "",
                seen = app.Now, room = room ?? "", roomFor = roomFor ?? "", roomAt = string.IsNullOrEmpty(room) ? 0 : app.Now,
            };
        }
    }

    // ===================================================================== local notifications

    public interface INotificationService
    {
        bool Supported { get; }
        void RequestPermission(Action<bool> done);
        void Schedule(int id, string title, string body, DateTime fireTimeLocal);
        void CancelAll();
    }

    /// <summary>
    /// Reminders while the player is away (chest ready, daily rewards...). Scheduled when the app goes to the
    /// background, all cancelled when it comes back. Off in Settings; never more than Tuning.notifyPerDay a day.
    /// </summary>
    public static class Reminders
    {
        public static INotificationService Service;
        const string AskedKey = "zoo_notify_asked";

        /// <summary>Asks the system permission once, at a good moment (after the first won match / first chest).</summary>
        public static void AskOnce(GameApp app)
        {
            if (Service == null || !Service.Supported || !app.Profile.notifications) return;
            if (PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
            Service.RequestPermission(ok => Debug.Log("[Notify] permission: " + ok));
        }

        public static void OnBackground(GameApp app)
        {
            if (Service == null || !Service.Supported) return;
            Service.CancelAll();
            if (!app.Profile.notifications || PlayerPrefs.GetInt(AskedKey, 0) == 0) return;
            int offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds;
            var plan = NotificationPlanner.Plan(app.Meta, app.Now, offset, Tuning.Current.notifyPerDay);
            long realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var n in plan)
            {
                long at = n.at - app.Now + realNow;   // the debug clock offset does not move real notifications
                var local = DateTimeOffset.FromUnixTimeSeconds(at).ToLocalTime().DateTime;
                string body;
                switch (n.kind)
                {
                    case NotifyKind.ChestReady: body = Loc.F("notify.chest", Loc.T("chest." + n.arg)); break;
                    case NotifyKind.LoginReward: body = Loc.F("notify.login", n.arg); break;
                    case NotifyKind.MonthlyCard: body = Loc.T("notify.monthly"); break;
                    case NotifyKind.ComeBack: body = Loc.T("notify.comeback"); break;
                    default: body = Loc.T("notify.missions"); break;
                }
                Service.Schedule(n.Id, Loc.T("notify.title"), body, local);
            }
        }

        public static void OnForeground() { if (Service != null && Service.Supported) Service.CancelAll(); }
    }
}
