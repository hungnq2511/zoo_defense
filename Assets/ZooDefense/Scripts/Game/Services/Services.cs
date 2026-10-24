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
        bool IsInterstitialReady { get; }
        /// <summary>Shows a full-screen ad between screens; onDone runs when it closes (or at once if none is ready).</summary>
        void ShowInterstitial(string placement, Action onDone);
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
        /// <summary>True when real products and prices are loaded from the App Store / Google Play.</summary>
        bool Ready { get; }
        /// <summary>Real store: charges for the product, grants it (Purchases.Fulfill) and returns the rewards; null = cancelled / failed.</summary>
        void Purchase(string productId, Action<List<ZooDefense.Core.Reward>> onDone);
        /// <summary>Localized store price ("49.000 ₫"), or the fallback while the store is not loaded.</summary>
        string Price(string productId, string fallback);
        /// <summary>Re-delivers purchases that were paid but never granted (and non-consumables).</summary>
        void Restore(Action<bool> onDone);
    }

    /// <summary>Editor / test store: every purchase succeeds for free.</summary>
    public sealed class FakeStore : IStoreService
    {
        public bool Ready { get { return false; } }
        public string Price(string productId, string fallback) { return fallback; }
        public void Restore(Action<bool> onDone) { if (onDone != null) onDone(true); }

        public void Purchase(string productId, Action<List<ZooDefense.Core.Reward>> onDone)
        {
            Debug.Log("[Store] (test) purchase " + productId + " -> success");
            var rewards = Purchases.Fulfill(productId, "test-" + Guid.NewGuid().ToString("N"));
            if (onDone != null) onDone(rewards);
        }
    }

    public enum ReceiptVerdict
    {
        /// <summary>The store's signed transaction checks out: grant it.</summary>
        Valid,
        /// <summary>Forged, for another app / product, refunded, or already used by another player: never grant.</summary>
        Invalid,
        /// <summary>Could not ask the server (offline, server error): keep the purchase pending and try again later.</summary>
        Unavailable,
    }

    /// <summary>Server-side check of a store transaction before the game grants it (Cloud Code "validate_purchase").</summary>
    public interface IReceiptValidator
    {
        /// <param name="store">"apple" or "google"</param>
        void Validate(string store, string productId, string transactionId, string jws, string receipt, Action<ReceiptVerdict, string> done);
    }

    public static class ReceiptValidation
    {
        /// <summary>Set by the ZooDefense.Online assembly; null = no server check.</summary>
        public static IReceiptValidator Service;
    }

    /// <summary>Grants paid products in one place (used by the shop and by purchases re-delivered at start-up).</summary>
    public static class Purchases
    {
        /// <summary>A paid product was granted outside a shop button (e.g. finished after a crash): show it.</summary>
        public static event Action<string, List<ZooDefense.Core.Reward>> Delivered;

        /// <summary>Grants and saves. Null = unknown product. Empty = already granted (duplicate delivery).</summary>
        public static List<ZooDefense.Core.Reward> Fulfill(string productId, string transactionId)
        {
            var app = GameApp.Instance;
            if (app == null || app.Meta == null) return null;
            var rewards = app.Meta.FulfillProduct(productId, transactionId, app.Now);
            if (rewards != null)
            {
                app.SaveProfile();
                Analytics.LogEvent("iap", new Dictionary<string, object> { { "product", productId } });
            }
            return rewards;
        }

        public static void RaiseDelivered(string productId, List<ZooDefense.Core.Reward> rewards)
        {
            if (Delivered != null) Delivered(productId, rewards);
        }

        static IAnalyticsService Analytics { get { return Services.Analytics; } }
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
        public bool IsInterstitialReady { get { return false; } }
        public void ShowInterstitial(string placement, Action onDone) { if (onDone != null) onDone(); }
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

    /// <summary>When a full-screen ad may appear: never for payers or new players, at most every few minutes.</summary>
    public static class AdPolicy
    {
        // Set from Remote Config (Tuning): ad_first_after, ad_every, ad_min_seconds.
        public static int FirstAfterMatches = 3;
        public static int EveryMatches = 2;
        public static float MinSeconds = 240f;
        static int _matchesAtLast = -1;
        static float _lastShown = -9999f;

        /// <summary>Call when leaving the result screen: maybe shows an interstitial, then runs `then`.</summary>
        public static void AfterMatch(GameApp app, Action then)
        {
            int played = app.Profile.matchesPlayed;
            bool ok = Services.Ads.IsInterstitialReady
                      && !app.Meta.IsPayer
                      && played >= FirstAfterMatches
                      && (_matchesAtLast < 0 || played - _matchesAtLast >= EveryMatches)
                      && Time.realtimeSinceStartup - _lastShown >= MinSeconds;
            if (!ok) { then(); return; }
            _matchesAtLast = played;
            _lastShown = Time.realtimeSinceStartup;
            Services.Ads.ShowInterstitial("after_match", then);
        }
    }
}
