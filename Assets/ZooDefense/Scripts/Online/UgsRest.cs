using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;
using ZooDefense.Core;

namespace ZooDefense.Online
{
    /// <summary>Signed-in REST calls to Unity Gaming Services (Remote Config, Leaderboards, Cloud Code): no extra packages.</summary>
    public static class UgsRest
    {
        public sealed class Response
        {
            public long code;
            public string text;
            public string error;
            public bool Ok { get { return error == null && code >= 200 && code < 300; } }
            public Dictionary<string, object> Json { get { return MiniJson.Object(text); } }
        }

        public static async Task<Response> Send(string method, string url, string jsonBody = null, Dictionary<string, string> headers = null)
        {
            await UgsCore.EnsureSignedInAsync();
            using (var req = new UnityWebRequest(url, method))
            {
                if (jsonBody != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
                if (headers != null) foreach (var kv in headers) req.SetRequestHeader(kv.Key, kv.Value);
                req.timeout = 15;
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                var r = new Response { code = req.responseCode, text = req.downloadHandler != null ? req.downloadHandler.text : "" };
                if (req.result != UnityWebRequest.Result.Success) r.error = req.error ?? ("http " + req.responseCode);
                return r;
            }
        }

        public static string ProjectId { get { return Application.cloudProjectId; } }
        public static string PlayerId { get { return AuthenticationService.Instance.PlayerId; } }

        /// <summary>Runs a Cloud Code script (CloudCode/*.js) and returns its "output" object.</summary>
        public static async Task<object> CloudCode(string script, Dictionary<string, object> parameters)
        {
            var body = MiniJson.Serialize(new Dictionary<string, object> { { "params", parameters ?? new Dictionary<string, object>() } });
            var r = await Send("POST", "https://cloud-code.services.api.unity.com/v1/projects/" + ProjectId + "/scripts/" + script, body);
            if (!r.Ok)
            {
                // Script errors come back as 422 with a JSON message.
                throw new Exception("cloud code " + script + ": " + r.code + " " + r.error + " " + Trim(r.text));
            }
            var json = r.Json;
            object output;
            return json != null && json.TryGetValue("output", out output) ? output : null;
        }

        static string Trim(string s) { return string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) : s); }

        /// <summary>Runs an async call and reports on the main thread (Unity's synchronization context).</summary>
        public static async void Run<T>(Func<Task<T>> work, Action<T> done, T fallback, string what)
        {
            T result;
            try { result = await work(); }
            catch (Exception e) { Debug.LogWarning("[" + what + "] " + e.Message); result = fallback; }
            if (done != null) done(result);
        }
    }

    /// <summary>Remote Config over its REST endpoint (same request the SDK sends). Keys: see LiveTuning.</summary>
    public sealed class UgsRemoteConfig : ZooDefense.Game.IRemoteConfigService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ZooDefense.Game.Tuning.Service = new UgsRemoteConfig(); }

        public void Fetch(Action<Dictionary<string, object>> done)
        {
            UgsRest.Run(async () =>
            {
                string platform = Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : (Application.platform == RuntimePlatform.Android ? "android" : "editor");
                var body = new Dictionary<string, object>
                {
                    { "projectId", UgsRest.ProjectId },
                    { "userId", UgsRest.PlayerId },
                    { "playerId", UgsRest.PlayerId },
                    { "isDebugBuild", Debug.isDebugBuild },
                    { "configType", "settings" },
                    { "analyticsUserId", "" },
                    { "environmentId", "" },
                    { "packageVersion", "4.0.4+RCR" },
                    { "attributes", new Dictionary<string, object>
                        {
                            { "unity", new Dictionary<string, object> { { "platform", platform }, { "appVersion", Application.version } } },
                            { "app", new Dictionary<string, object> { { "version", Application.version } } },
                            { "user", new Dictionary<string, object>() },
                        }
                    },
                };
                var headers = new Dictionary<string, string> { { "unity-player-id", UgsRest.PlayerId } };
                var r = await UgsRest.Send("POST", "https://config.services.api.unity.com/settings", MiniJson.Serialize(body), headers);
                if (!r.Ok) throw new Exception(r.code + " " + r.error);
                return MiniJson.Obj(MiniJson.Obj(r.Json, "configs"), "settings");
            }, done, null, "RemoteConfig");
        }
    }
}
