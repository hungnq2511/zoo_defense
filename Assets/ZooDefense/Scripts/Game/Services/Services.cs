using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZooDefense.Game
{
    // Thin interfaces in front of every outside SDK, so the providers (Unity Gaming Services,
    // LevelPlay ads, Unity IAP, analytics) can be plugged in or swapped without touching gameplay.
    // The prototype ships with offline stand-ins that only log.

    public interface IAnalyticsService
    {
        void LogEvent(string name, IDictionary<string, object> parameters = null);
    }

    public interface IAdsService
    {
        bool IsRewardedReady { get; }
        /// <summary>Shows a rewarded ad. Callback receives true when the reward should be granted.</summary>
        void ShowRewarded(string placement, Action<bool> onDone);
    }

    public interface IBackendService
    {
        /// <summary>Later: UGS Authentication + Cloud Save. Now: local only.</summary>
        string PlayerId { get; }
        /// <summary>Later: upload MatchRecord to Cloud Code for validation before rewards.</summary>
        void SubmitMatch(ZooDefense.Core.MatchRecord record, Action<bool> onValidated);
    }

    public interface IStoreService
    {
        /// <summary>Starts a real-money purchase. Later: Unity IAP + server receipt validation.</summary>
        void Purchase(string productId, Action<bool> onDone);
    }

    public sealed class FakeStore : IStoreService
    {
        public void Purchase(string productId, Action<bool> onDone)
        {
            Debug.Log("[Store] (stub) purchase " + productId + " -> success");
            if (onDone != null) onDone(true);
        }
    }

    public sealed class LogAnalytics : IAnalyticsService
    {
        public void LogEvent(string name, IDictionary<string, object> parameters = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var text = name;
            if (parameters != null) foreach (var kv in parameters) text += " " + kv.Key + "=" + kv.Value;
            Debug.Log("[Analytics] " + text);
#endif
        }
    }

    public sealed class FakeAds : IAdsService
    {
        public bool IsRewardedReady { get { return true; } }
        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            Debug.Log("[Ads] (stub) rewarded ad '" + placement + "' -> granted");
            if (onDone != null) onDone(true);
        }
    }

    public sealed class OfflineBackend : IBackendService
    {
        public string PlayerId { get { return SystemInfo.deviceUniqueIdentifier; } }

        public void SubmitMatch(ZooDefense.Core.MatchRecord record, Action<bool> onValidated)
        {
            // Same check the server will run in Cloud Code.
            var result = ZooDefense.Core.ReplayValidator.Validate(GameApp.Instance.Content, record);
            if (!result.valid) Debug.LogWarning("[Backend] replay validation failed: " + result.error);
            if (onValidated != null) onValidated(result.valid);
        }
    }

    public static class Services
    {
        public static IAnalyticsService Analytics = new LogAnalytics();
        public static IAdsService Ads = new FakeAds();
        public static IBackendService Backend = new OfflineBackend();
        public static IStoreService Store = new FakeStore();
    }
}
