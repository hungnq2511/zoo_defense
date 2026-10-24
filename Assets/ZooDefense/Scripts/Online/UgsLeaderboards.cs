using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ZooDefense.Core;
using ZooDefense.Game;

namespace ZooDefense.Online
{
    /// <summary>
    /// Unity Leaderboards over REST. Reading is direct; writing goes through Cloud Code "submit_score"
    /// (CloudCode/submit_score.js), which checks the value, so players cannot post any score they like
    /// (in the dashboard, client writes to the boards are denied by an access-control rule).
    /// Boards: trophies (keep latest), endless_weekly (reset every Monday 00:00 UTC), daily_challenge (reset daily).
    /// </summary>
    public sealed class UgsLeaderboards : ILeaderboardService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { Leaderboards.Service = new UgsLeaderboards(); }

        public bool Available { get { return Application.internetReachability != NetworkReachability.NotReachable; } }

        static string Base(LeaderboardKind kind)
        {
            return "https://leaderboards.services.api.unity.com/v1/projects/" + UgsRest.ProjectId + "/leaderboards/" + Leaderboards.Id(kind) + "/scores";
        }

        public void Submit(LeaderboardKind kind, long score, Dictionary<string, object> info)
        {
            var p = new Dictionary<string, object> { { "board", Leaderboards.Id(kind) }, { "score", score } };
            if (info != null) foreach (var kv in info) p[kv.Key] = kv.Value;
            UgsRest.Run<object>(() => UgsRest.CloudCode("submit_score", p), r => Debug.Log("[Leaderboards] " + Leaderboards.Id(kind) + " = " + score), null, "Leaderboards");
        }

        public void Top(LeaderboardKind kind, int limit, Action<List<LeaderboardEntry>> done)
        {
            Get(Base(kind) + "?offset=0&limit=" + Mathf.Clamp(limit, 1, 100) + "&includeMetadata=true", "GET", null, done);
        }

        public void AroundMe(LeaderboardKind kind, int range, Action<List<LeaderboardEntry>> done)
        {
            UgsRest.Run(async () =>
            {
                var r = await UgsRest.Send("GET", Base(kind) + "/players/" + UgsRest.PlayerId + "/range?rangeLimit=" + Mathf.Clamp(range, 1, 50) + "&includeMetadata=true");
                // Not on the board yet (404): show the top instead.
                if (r.code == 404) r = await UgsRest.Send("GET", Base(kind) + "?offset=0&limit=20&includeMetadata=true");
                if (!r.Ok) throw new Exception(r.code + " " + r.error);
                return Parse(r.Json);
            }, done, null, "Leaderboards");
        }

        public void Players(LeaderboardKind kind, List<string> playerIds, Action<List<LeaderboardEntry>> done)
        {
            // One GET per player (reads only): the access-control rule denies client writes to player scores,
            // and the batch endpoint is a POST.
            UgsRest.Run(async () =>
            {
                var tasks = new List<Task<UgsRest.Response>>();
                var ids = new List<string>(playerIds);
                if (ids.Count > 60) ids.RemoveRange(60, ids.Count - 60);
                foreach (var id in ids)
                    tasks.Add(UgsRest.Send("GET", Base(kind) + "/players/" + Uri.EscapeDataString(id) + "?includeMetadata=true"));
                await Task.WhenAll(tasks);
                var list = new List<LeaderboardEntry>();
                foreach (var t in tasks)
                {
                    var r = t.Result;
                    if (!r.Ok) continue;   // 404: not on this board yet
                    var one = Parse(new Dictionary<string, object> { { "results", new List<object> { r.Json } } });
                    list.AddRange(one);
                }
                // A friend list is ranked among the friends.
                list.Sort((x, y) => y.score.CompareTo(x.score));
                for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
                return list;
            }, done, null, "Leaderboards");
        }

        void Get(string url, string method, string body, Action<List<LeaderboardEntry>> done)
        {
            UgsRest.Run(async () =>
            {
                var r = await UgsRest.Send(method, url, body);
                if (r.code == 404) return new List<LeaderboardEntry>();   // empty / new board
                if (!r.Ok) throw new Exception(r.code + " " + r.error);
                return Parse(r.Json);
            }, done, null, "Leaderboards");
        }

        static List<LeaderboardEntry> Parse(Dictionary<string, object> json)
        {
            var list = new List<LeaderboardEntry>();
            var results = MiniJson.Arr(json, "results");
            if (results == null) return list;
            string me = UgsRest.PlayerId;
            foreach (var o in results)
            {
                var row = o as Dictionary<string, object>;
                if (row == null) continue;
                object metaRaw;
                Dictionary<string, object> meta = null;
                if (row.TryGetValue("metadata", out metaRaw))
                    meta = metaRaw as Dictionary<string, object> ?? MiniJson.Object(metaRaw as string);
                var e = new LeaderboardEntry
                {
                    playerId = MiniJson.Str(row, "playerId", ""),
                    rank = MiniJson.Int(row, "rank") + 1,
                    score = (long)MiniJson.Num(row, "score"),
                    name = MiniJson.Str(meta, "n", null) ?? StripTag(MiniJson.Str(row, "playerName", "")),
                    title = MiniJson.Str(meta, "t", ""),
                    arena = MiniJson.Int(meta, "a", 1),
                };
                e.me = e.playerId == me;
                list.Add(e);
            }
            return list;
        }

        /// <summary>Unity player names look like "Name#1234": the game shows the part before '#'.</summary>
        static string StripTag(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int i = n.LastIndexOf('#');
            return i > 0 ? n.Substring(0, i) : n;
        }
    }
}
