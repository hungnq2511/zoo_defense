using System;
using UnityEngine;

namespace ZooDefense.Monetization
{
    /// <summary>
    /// Ad keys from Resources/ZooMonetization.json (LevelPlay dashboard: App Key + ad unit ids per platform).
    /// Empty keys = no real ads (the editor keeps the test ads).
    /// </summary>
    [Serializable]
    public class MonetizationConfig
    {
        public string levelPlayAppKeyIos = "";
        public string levelPlayAppKeyAndroid = "";
        public string rewardedAdUnitIos = "";
        public string rewardedAdUnitAndroid = "";
        public string interstitialAdUnitIos = "";
        public string interstitialAdUnitAndroid = "";
        /// <summary>Opens the LevelPlay Test Suite after start-up (device builds only). Turn off before release.</summary>
        public bool testSuite;
        /// <summary>Check every purchase on the server (Cloud Code "validate_purchase") before granting it. Turn on once the script is deployed.</summary>
        public bool serverValidation;

        static MonetizationConfig _loaded;

        public static MonetizationConfig Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = new MonetizationConfig();
            var asset = Resources.Load<TextAsset>("ZooMonetization");
            if (asset != null)
            {
                try { JsonUtility.FromJsonOverwrite(asset.text, _loaded); }
                catch (Exception e) { Debug.LogWarning("[Ads] ZooMonetization.json invalid: " + e.Message); }
            }
            return _loaded;
        }

        bool Ios { get { return Application.platform == RuntimePlatform.IPhonePlayer || (Application.isEditor && IsEditorTargetIos()); } }

        static bool IsEditorTargetIos()
        {
#if UNITY_IOS
            return true;
#else
            return false;
#endif
        }

        public string AppKey { get { return Pick(levelPlayAppKeyIos, levelPlayAppKeyAndroid); } }
        public string RewardedUnit { get { return Pick(rewardedAdUnitIos, rewardedAdUnitAndroid); } }
        public string InterstitialUnit { get { return Pick(interstitialAdUnitIos, interstitialAdUnitAndroid); } }

        /// <summary>The value for this platform. In the editor, falls back to the other platform's value (mock ads).</summary>
        string Pick(string ios, string android)
        {
            var v = Ios ? ios : android;
            if (string.IsNullOrEmpty(v) && Application.isEditor) v = Ios ? android : ios;
            return v;
        }
    }
}
