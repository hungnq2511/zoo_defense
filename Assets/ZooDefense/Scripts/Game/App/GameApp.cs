using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Root of the game: loads content and profile, owns the camera, the UI canvas and the current screen.
    /// Put one on an empty GameObject in a scene (Zoo Defense > Create Main Scene does it), or just press
    /// Play in any scene: ZooBootstrap creates it automatically.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public ContentDatabase Content { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public MetaConfig MetaConfig { get; private set; }
        /// <summary>Economy operations on the profile's PlayerState. Save the profile after using it.</summary>
        public MetaService Meta { get; private set; }

        /// <summary>Current time in unix seconds (plus the test offset from Settings).</summary>
        public long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (Profile != null ? Profile.debugTimeOffset : 0); } }
        public Camera Cam { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }

        CanvasScaler _scaler;
        RectTransform _safeArea;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;
        ScreenBase _screen;
        Image _toastBg;
        Text _toastText;
        float _toastTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;

            Content = ContentLoader.Load();
            MetaConfig = DefaultMeta.Create();
            Profile = ProfileStore.Load(Content, MetaConfig);
            Meta = new MetaService(MetaConfig, Content, Profile.meta);
            Meta.Refresh(Now);
            Loc.Init(Profile.language);
            CreateCamera();
            CreateCanvas();
            EnsureEventSystem();
            gameObject.AddComponent<AudioService>().Init(this);
        }

        void Start()
        {
            Services.Analytics.LogEvent("app_start");
            // Sign in in the background (guest or linked account) so online play and backups are ready.
            if (Accounts.Service != null) Accounts.Service.Refresh(null);
            // A purchase finished outside the shop (app was closed while paying): show what arrived.
            Purchases.Delivered += (id, rewards) =>
            {
                if (rewards != null && rewards.Count > 0) Modal.Rewards(this, Loc.T("shop.delivered"), rewards);
            };
            Show(new SplashScreen());
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(ScreenBase next)
        {
            if (_screen != null) _screen.Close();
            Modal.CloseAll();
            UIKit.Clear(ScreenLayer);
            _screen = next;
            _screenRoot = UIKit.Rect(next.GetType().Name, ScreenLayer).Stretch();
            _screen.Open(this, _screenRoot);
            RefreshBleed();
        }

        // ------------------------------------------------------------------ full-screen backgrounds (see SafeBleed)
        RectTransform _screenRoot;
        int _bleedScreenCount = -1, _bleedOverlayCount = -1;

        void RefreshBleed()
        {
            SafeBleed.Apply(_screenRoot);
            SafeBleed.Apply(OverlayLayer);
            _bleedScreenCount = _screenRoot != null ? _screenRoot.childCount : -1;
            _bleedOverlayCount = OverlayLayer != null ? OverlayLayer.childCount : -1;
        }

        void WatchBleed()
        {
            // New full-screen panels / dialogs appear after Build (popups, modals): extend them too.
            if ((_screenRoot != null && _screenRoot.childCount != _bleedScreenCount) || (OverlayLayer != null && OverlayLayer.childCount != _bleedOverlayCount))
                RefreshBleed();
        }

        public void SaveProfile()
        {
            ProfileStore.Save(Profile);
            if (Accounts.Service != null) Accounts.Service.MarkDirty();   // cloud backup (batched) when linked
        }

        /// <summary>Replaces this phone's progress with a cloud save (account sign-in on a new phone). Keeps device settings.</summary>
        public bool ReplaceProfile(string json)
        {
            var p = ProfileStore.FromJson(json, Content, MetaConfig);
            if (p == null) return false;
            p.language = Profile.language;
            p.musicVolume = Profile.musicVolume;
            p.sfxVolume = Profile.sfxVolume;
            p.debugTimeOffset = 0;
            Profile = p;
            Meta = new MetaService(MetaConfig, Content, Profile.meta);
            Meta.Refresh(Now);
            ProfileStore.Save(Profile);
            return true;
        }

        void OnApplicationPause(bool paused)
        {
            // Going to the background: back up now (the app may be closed from there).
            if (paused && Accounts.Service != null && Accounts.Service.IsLinked && !Accounts.Service.Busy) Accounts.Service.BackupNow(null);
        }

        public void ResetProfile()
        {
            var language = Profile.language;
            ProfileStore.Reset();
            Profile = ProfileStore.Load(Content, MetaConfig);
            Profile.language = language;   // keep the chosen language
            Meta = new MetaService(MetaConfig, Content, Profile.meta);
            Meta.Refresh(Now);
            SaveProfile();
        }

        void Update()
        {
            KeepLayout();
            if (_screen != null) _screen.Tick(Time.deltaTime);
            WatchBleed();
            if (Accounts.Service != null) Accounts.Service.Update(Time.unscaledDeltaTime);
            if (_toastTime > 0f)
            {
                _toastTime -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(_toastTime * 3f);
                _toastBg.color = new Color(0f, 0f, 0f, 0.75f * a);
                _toastText.color = new Color(1f, 1f, 1f, a);
                if (_toastTime <= 0f) _toastBg.gameObject.SetActive(false);
            }
        }

        public void Toast(string message, float seconds = 1.6f)
        {
            _toastText.text = message;
            _toastBg.gameObject.SetActive(true);
            _toastBg.transform.SetAsLastSibling();
            _toastTime = seconds;
        }

        // ------------------------------------------------------------------ setup

        void CreateCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Cam = go.AddComponent<Camera>();
            }
            if (Cam.GetComponent<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Background;
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.transform.rotation = Quaternion.identity;
            DontDestroyOnLoad(Cam.gameObject);
        }

        void CreateCanvas()
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.layer = UIKit.UILayer;
            DontDestroyOnLoad(go);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = go.AddComponent<CanvasScaler>();
            // Scale factor is set in KeepLayout so the SAFE AREA is always at least 1080 x 1920 UI units.
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();

            _safeArea = UIKit.Rect("SafeArea", go.transform).Stretch();
            ScreenLayer = UIKit.Rect("Screens", _safeArea).Stretch();
            OverlayLayer = UIKit.Rect("Overlay", _safeArea).Stretch();

            _toastBg = UIKit.Panel(OverlayLayer, "Toast", new Color(0f, 0f, 0f, 0.75f));
            _toastBg.rectTransform.Place(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 110f));
            _toastText = UIKit.Label(_toastBg.transform, "", 40, Color.white);
            _toastText.rectTransform.Stretch(20, 20, 0, 0);
            _toastBg.gameObject.SetActive(false);
            KeepLayout();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            Debug.LogError("Zoo Defense prototype uses the legacy Input Manager. Set Edit > Project Settings > Player > " +
                           "Active Input Handling to 'Both' (or 'Input Manager (Old)').");
#endif
            DontDestroyOnLoad(go);
        }

        /// <summary>Reference layout is 1080 x 1920 (9:16 portrait).</summary>
        public static readonly Vector2 ReferenceSize = new Vector2(1080f, 1920f);

        /// <summary>
        /// The UI is designed for a 1080 x 1920 portrait area. The scale is chosen so the safe area (screen minus
        /// notch / home bar) is never smaller than that in either direction: tall phones (9:19.5, 9:20) get extra
        /// height, tablets (3:4, 10:16) extra width, and nothing is ever cut off or squeezed.
        /// </summary>
        void KeepLayout()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            var sa = Screen.safeArea;
            if ((size != _lastScreen || sa != _lastSafeArea) && Screen.width > 0 && Screen.height > 0)
            {
                _lastScreen = size;
                _lastSafeArea = sa;
                float w = sa.width > 1f ? sa.width : Screen.width;
                float h = sa.height > 1f ? sa.height : Screen.height;
                _scaler.scaleFactor = Mathf.Max(0.01f, Mathf.Min(w / ReferenceSize.x, h / ReferenceSize.y));
                _safeArea.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                _safeArea.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
                float k = _scaler.scaleFactor;
                SafeBleed.Insets = new Vector4(sa.xMin / k, sa.yMin / k, (Screen.width - sa.xMax) / k, (Screen.height - sa.yMax) / k);
                if (_screenRoot != null) RefreshBleed();
            }
        }
    }

    /// <summary>Base class for code-built screens.</summary>
    public abstract class ScreenBase
    {
        protected GameApp App { get; private set; }
        protected RectTransform Root { get; private set; }

        public void Open(GameApp app, RectTransform root)
        {
            App = app;
            Root = root;
            Build();
        }

        protected abstract void Build();
        public virtual void Tick(float dt) { }
        public virtual void Close() { }
    }

    public static class ContentLoader
    {
        public const string ResourcePath = "ZooBalance";

        /// <summary>Balance JSON in Resources overrides the code defaults (export it from the Zoo Defense menu).</summary>
        public static ContentDatabase Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null)
            {
                try
                {
                    var data = JsonUtility.FromJson<ContentData>(asset.text);
                    var db = new ContentDatabase(data);
                    Debug.Log("[ZooDefense] Loaded balance from Resources/" + ResourcePath + ".json");
                    return db;
                }
                catch (Exception e)
                {
                    Debug.LogError("[ZooDefense] Balance JSON invalid, using defaults: " + e.Message);
                }
            }
            return DefaultContent.CreateDatabase();
        }
    }

    static class ZooBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureApp()
        {
#if !ZOO_NO_AUTOBOOT
            if (UnityEngine.Object.FindAnyObjectByType<GameApp>() == null)
                new GameObject("GameApp").AddComponent<GameApp>();
#endif
        }
    }
}
