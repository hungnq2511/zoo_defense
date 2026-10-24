#if ZOO_ANALYTICS
using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using UnityEngine;
using ZooDefense.Game;

namespace ZooDefense.Online
{
    /// <summary>
    /// Unity Analytics (dashboard: Analytics > Event Browser). Data is collected only after the player agreed in the
    /// privacy dialog (Privacy.Personalized); saying no, or switching it off in Settings, stops collection.
    /// Events before the services are ready are kept (a few) and sent once collection starts.
    /// Every custom event and parameter must exist in the dashboard (Event Manager), see Docs/LIVE_SERVICES.md.
    /// </summary>
    public sealed class UgsAnalytics : ZooDefense.Game.IAnalyticsService
    {
        const int MaxQueued = 60;
        static UgsAnalytics _instance;
        readonly LogAnalytics _log = new LogAnalytics();
        readonly List<KeyValuePair<string, IDictionary<string, object>>> _queue = new List<KeyValuePair<string, IDictionary<string, object>>>();
        bool _ready, _collecting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Register()
        {
            _instance = new UgsAnalytics();
            Services.Analytics = _instance;
            Privacy.Changed += () => _instance.ApplyConsent();
            try
            {
                await UgsCore.EnsureSignedInAsync();
                _instance._ready = true;
                _instance.ApplyConsent();
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] not started: " + e.Message); }
        }

        void ApplyConsent()
        {
            if (!_ready) return;
            bool want = Privacy.Decided && Privacy.Personalized;
            try
            {
                if (want && !_collecting)
                {
                    AnalyticsService.Instance.StartDataCollection();
                    _collecting = true;
                    foreach (var e in _queue) Send(e.Key, e.Value);
                    _queue.Clear();
                }
                else if (!want && _collecting)
                {
                    AnalyticsService.Instance.StopDataCollection();
                    _collecting = false;
                }
                if (!want) _queue.Clear();
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] consent: " + e.Message); }
        }

        public void LogEvent(string name, IDictionary<string, object> parameters = null)
        {
            _log.LogEvent(name, parameters);
            if (_collecting) { Send(name, parameters); return; }
            // Not decided yet: keep a few (the very first session matters). Declined: nothing is kept.
            if (Privacy.Decided && !Privacy.Personalized) return;
            if (_queue.Count < MaxQueued) _queue.Add(new KeyValuePair<string, IDictionary<string, object>>(name, parameters));
        }

        static void Send(string name, IDictionary<string, object> parameters)
        {
            try
            {
                var e = new CustomEvent(name);
                if (parameters != null)
                    foreach (var kv in parameters)
                    {
                        if (kv.Value == null) continue;
                        object v = kv.Value;
                        // Only string / int / long / float / double / bool are allowed.
                        if (!(v is string || v is int || v is long || v is float || v is double || v is bool)) v = v.ToString();
                        if (v is string && ((string)v).Length > 250) v = ((string)v).Substring(0, 250);
                        e.Add(kv.Key, v);
                    }
                AnalyticsService.Instance.RecordEvent(e);
            }
            catch (Exception ex) { Debug.LogWarning("[Analytics] " + name + ": " + ex.Message); }
        }
    }
}
#endif
