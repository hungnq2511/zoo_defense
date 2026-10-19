using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZooDefense.Game
{
    /// <summary>
    /// The player's choice about personalised ads (GDPR consent / CCPA "do not sell or share").
    /// Asked once on first launch for everyone, changeable in Settings. Stored on this device.
    /// The ad SDK waits for the choice before it starts.
    /// </summary>
    public static class Privacy
    {
        const string DecidedKey = "zoo_privacy_decided";
        const string PersonalizedKey = "zoo_privacy_personalized";

        /// <summary>Raised when the player answers or changes the choice.</summary>
        public static event Action Changed;

        public static bool Decided { get { return PlayerPrefs.GetInt(DecidedKey, 0) == 1; } }
        /// <summary>True = the player agreed to personalised ads (and to sharing data with ad partners for that).</summary>
        public static bool Personalized { get { return PlayerPrefs.GetInt(PersonalizedKey, 0) == 1; } }

        public static void Set(bool personalized)
        {
            PlayerPrefs.SetInt(DecidedKey, 1);
            PlayerPrefs.SetInt(PersonalizedKey, personalized ? 1 : 0);
            PlayerPrefs.Save();
            Services.Analytics.LogEvent("privacy_choice", new System.Collections.Generic.Dictionary<string, object> { { "personalized", personalized } });
            if (Changed != null) Changed();
        }

        [Serializable]
        class Links { public string privacyPolicyUrl = ""; public string termsUrl = ""; }
        static Links _links;

        static Links Load()
        {
            if (_links != null) return _links;
            _links = new Links();
            var asset = Resources.Load<TextAsset>("ZooMonetization");
            if (asset != null) { try { JsonUtility.FromJsonOverwrite(asset.text, _links); } catch (Exception) { } }
            return _links;
        }

        /// <summary>Privacy policy page (Resources/ZooMonetization.json "privacyPolicyUrl"); empty = none yet.</summary>
        public static string PolicyUrl { get { return Load().privacyPolicyUrl; } }

        /// <summary>First launch: the consent dialog (can't be skipped by tapping outside; × = no personalised ads).</summary>
        public static void AskIfNeeded(GameApp app)
        {
            if (Decided) return;
            Show(app);
        }

        public static void Show(GameApp app)
        {
            var box = Modal.Open(app, Loc.T("privacy.title"), 900, () => { if (!Decided) Set(false); });
            UIKit.Label(box, Loc.T("privacy.text"), 30, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(50, 50, 140, 330);
            var yes = UIKit.ChunkyButton(box, "yes", Loc.T("privacy.accept"), Palette.Green, () => { Set(true); Modal.CloseFrom(box); }, 34);
            yes.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(760, 116));
            var no = UIKit.ChunkyButton(box, "no", Loc.T("privacy.decline"), Art.Hex(0x4A3D66), () => { Set(false); Modal.CloseFrom(box); }, 30);
            no.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(760, 100));
            if (!string.IsNullOrEmpty(PolicyUrl))
            {
                var link = UIKit.Button(box, "policy", Loc.T("privacy.policy"), new Color(0f, 0f, 0f, 0f), () => Application.OpenURL(PolicyUrl), 24);
                link.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(600, 50));
                link.GetComponentInChildren<Text>().color = Palette.TextDim;
            }
        }
    }
}
