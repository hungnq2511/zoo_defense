#if ZOO_LEVELPLAY
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Services.LevelPlay;
using UnityEngine;
using ZooDefense.Game;

namespace ZooDefense.Monetization
{
    /// <summary>
    /// Real ads through LevelPlay (Ads Mediation): rewarded (free gems, double gold) and an interstitial between
    /// matches (AdPolicy decides when). iOS asks for tracking permission (ATT) before starting the SDK.
    /// </summary>
    public sealed class LevelPlayAds : MonoBehaviour, IAdsService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            var cfg = MonetizationConfig.Load();
            if (string.IsNullOrEmpty(cfg.AppKey))
            {
                Debug.Log("[Ads] No LevelPlay app key in ZooMonetization.json: test ads only.");
                return;
            }
            var go = new GameObject("LevelPlayAds");
            DontDestroyOnLoad(go);
            Services.Ads = go.AddComponent<LevelPlayAds>();
        }

        MonetizationConfig _cfg;
        LevelPlayRewardedAd _rewarded;
        LevelPlayInterstitialAd _interstitial;
        Action<bool> _rewardDone;
        bool _earned;
        Action _interDone;
        float _rewardedRetry = 5f, _interRetry = 5f;

        public bool IsRewardedReady { get { return _rewarded != null && _rewarded.IsAdReady(); } }
        public bool IsInterstitialReady { get { return _interstitial != null && _interstitial.IsAdReady(); } }

#if UNITY_IOS && !UNITY_EDITOR
        delegate void ATTCallbackDelegate(int status);
        [DllImport("__Internal")] static extern int _ATT_GetStatus();
        [DllImport("__Internal")] static extern void _ATT_RequestPermission(ATTCallbackDelegate callback);
        static bool _attDone;

        [AOT.MonoPInvokeCallback(typeof(ATTCallbackDelegate))]
        static void OnATTCallback(int status)
        {
            Debug.Log("[Ads] ATT status " + status);
            _attDone = true;
        }

        IEnumerator RequestATT()
        {
            if (_ATT_GetStatus() != 0) yield break;   // already answered
            _attDone = false;
            _ATT_RequestPermission(OnATTCallback);
            yield return new WaitUntil(() => _attDone);
        }
#endif

        IEnumerator Start()
        {
            _cfg = MonetizationConfig.Load();
            // The player's privacy choice comes first (asked on the first launch, before any ad).
            yield return new WaitUntil(() => Privacy.Decided);
#if UNITY_IOS && !UNITY_EDITOR
            // Only players who accepted personalised ads get Apple's tracking prompt.
            if (Privacy.Personalized) yield return RequestATT();
#else
            yield return null;
#endif
            ApplyPrivacy();
            Privacy.Changed += ApplyPrivacy;
            if (_cfg.testSuite) LevelPlay.SetMetaData("is_test_suite", "enable");
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(_cfg.AppKey);
        }

        /// <summary>GDPR consent and CCPA "do not sell or share" from the player's choice (before Init, and on change).</summary>
        void ApplyPrivacy()
        {
            bool personalized = Privacy.Personalized;
            LevelPlayPrivacySettings.SetGDPRConsent(personalized);
            LevelPlayPrivacySettings.SetCCPA(!personalized);
            Debug.Log("[Ads] personalised ads: " + personalized);
        }

        void OnInitSuccess(LevelPlayConfiguration config)
        {
            Debug.Log("LevelPlay SDK initialized successfully");
            if (_cfg.testSuite) LevelPlay.LaunchTestSuite();

            if (!string.IsNullOrEmpty(_cfg.RewardedUnit))
            {
                _rewarded = new LevelPlayRewardedAd(_cfg.RewardedUnit);
                _rewarded.OnAdLoaded += OnRewardedLoaded;
                _rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
                _rewarded.OnAdDisplayed += OnAnyDisplayed;
                _rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
                _rewarded.OnAdRewarded += OnRewardedRewarded;
                _rewarded.OnAdClosed += OnRewardedClosed;
                _rewarded.LoadAd();
            }
            if (!string.IsNullOrEmpty(_cfg.InterstitialUnit))
            {
                _interstitial = new LevelPlayInterstitialAd(_cfg.InterstitialUnit);
                _interstitial.OnAdLoaded += OnInterLoaded;
                _interstitial.OnAdLoadFailed += OnInterLoadFailed;
                _interstitial.OnAdDisplayed += OnAnyDisplayed;
                _interstitial.OnAdDisplayFailed += OnInterDisplayFailed;
                _interstitial.OnAdClosed += OnInterClosed;
                _interstitial.LoadAd();
            }
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogError("LevelPlay initialization failed: " + error.ErrorMessage);
        }

        // ------------------------------------------------------------------ rewarded

        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            if (!IsRewardedReady) { if (onDone != null) onDone(false); return; }
            _rewardDone = onDone;
            _earned = false;
            Services.Analytics.LogEvent("ad_rewarded_show", new System.Collections.Generic.Dictionary<string, object> { { "placement", placement } });
            _rewarded.ShowAd();
        }

        void OnRewardedLoaded(LevelPlayAdInfo info) { _rewardedRetry = 5f; }

        void OnRewardedLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] rewarded load failed: " + error.ErrorMessage);
            Invoke(nameof(LoadRewarded), _rewardedRetry);
            _rewardedRetry = Mathf.Min(_rewardedRetry * 2f, 120f);
        }

        void LoadRewarded() { if (_rewarded != null) _rewarded.LoadAd(); }

        void OnRewardedRewarded(LevelPlayAdInfo info, LevelPlayReward reward) { _earned = true; }

        void OnRewardedClosed(LevelPlayAdInfo info)
        {
            AudioListener.pause = false;
            // Some networks report the reward just after the close: wait a moment before deciding.
            StartCoroutine(FinishRewarded());
            LoadRewarded();
        }

        IEnumerator FinishRewarded()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            var done = _rewardDone;
            _rewardDone = null;
            if (done != null) done(_earned);
        }

        void OnRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] rewarded display failed: " + error.ErrorMessage);
            AudioListener.pause = false;
            var done = _rewardDone;
            _rewardDone = null;
            if (done != null) done(false);
            LoadRewarded();
        }

        // ------------------------------------------------------------------ interstitial

        public void ShowInterstitial(string placement, Action onDone)
        {
            if (!IsInterstitialReady) { if (onDone != null) onDone(); return; }
            _interDone = onDone;
            Services.Analytics.LogEvent("ad_interstitial_show", new System.Collections.Generic.Dictionary<string, object> { { "placement", placement } });
            _interstitial.ShowAd();
        }

        void OnInterLoaded(LevelPlayAdInfo info) { _interRetry = 5f; }

        void OnInterLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] interstitial load failed: " + error.ErrorMessage);
            Invoke(nameof(LoadInterstitial), _interRetry);
            _interRetry = Mathf.Min(_interRetry * 2f, 120f);
        }

        void LoadInterstitial() { if (_interstitial != null) _interstitial.LoadAd(); }

        void OnInterClosed(LevelPlayAdInfo info)
        {
            AudioListener.pause = false;
            FinishInterstitial();
            LoadInterstitial();
        }

        void OnInterDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] interstitial display failed: " + error.ErrorMessage);
            AudioListener.pause = false;
            FinishInterstitial();
            LoadInterstitial();
        }

        void FinishInterstitial()
        {
            var done = _interDone;
            _interDone = null;
            if (done != null) done();
        }

        void OnAnyDisplayed(LevelPlayAdInfo info) { AudioListener.pause = true; }

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            Privacy.Changed -= ApplyPrivacy;
            if (_rewarded != null)
            {
                _rewarded.OnAdLoaded -= OnRewardedLoaded;
                _rewarded.OnAdLoadFailed -= OnRewardedLoadFailed;
                _rewarded.OnAdDisplayed -= OnAnyDisplayed;
                _rewarded.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                _rewarded.OnAdRewarded -= OnRewardedRewarded;
                _rewarded.OnAdClosed -= OnRewardedClosed;
            }
            if (_interstitial != null)
            {
                _interstitial.OnAdLoaded -= OnInterLoaded;
                _interstitial.OnAdLoadFailed -= OnInterLoadFailed;
                _interstitial.OnAdDisplayed -= OnAnyDisplayed;
                _interstitial.OnAdDisplayFailed -= OnInterDisplayFailed;
                _interstitial.OnAdClosed -= OnInterClosed;
                _interstitial.DestroyAd();
            }
        }
    }
}
#endif
