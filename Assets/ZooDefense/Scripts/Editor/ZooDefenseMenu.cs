using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZooDefense.Core;
using ZooDefense.Game;

namespace ZooDefense.EditorTools
{
    /// <summary>Menu: Zoo Defense/... - project setup, balance export and art checks.</summary>
    public static class ZooDefenseMenu
    {
        public const string ScenePath = "Assets/ZooDefense/Scenes/Main.unity";
        public const string BalancePath = "Assets/ZooDefense/Resources/ZooBalance.json";
        public const string ArtRoot = "Assets/ZooDefense/Resources/ZooArt";

        [MenuItem("Zoo Defense/Create Main Scene", priority = 1)]
        public static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = 7f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Palette.Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            new GameObject("GameApp").AddComponent<GameApp>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[ZooDefense] Created " + ScenePath + " and added it to Build Settings. Press Play.");
        }

        [MenuItem("Zoo Defense/Apply Mobile Player Settings", priority = 2)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Zoo Defense";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            // Desktop / editor test builds open as a 9:16 portrait window too.
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            // App icon (default icon for every platform): Assets/ZooDefense/Branding/app_icon.png
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ZooDefense/Branding/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            Debug.Log("[ZooDefense] Portrait orientation (1080x1920) and 2D editor mode applied. Test other phones in Window > General > Device Simulator. Set your bundle id in Player Settings before building.");
        }

        [MenuItem("Zoo Defense/Balance/Export Balance JSON", priority = 20)]
        public static void ExportBalance()
        {
            var data = DefaultContent.Create();
            Directory.CreateDirectory(Path.GetDirectoryName(BalancePath));
            File.WriteAllText(BalancePath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(BalancePath);
            Debug.Log("[ZooDefense] Wrote " + BalancePath + ". Edit numbers there; the game loads it instead of the code defaults.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(BalancePath));
        }

        [MenuItem("Zoo Defense/Balance/Validate Balance JSON", priority = 21)]
        public static void ValidateBalance()
        {
            if (!File.Exists(BalancePath)) { Debug.Log("[ZooDefense] No balance JSON: code defaults are used."); return; }
            try
            {
                var db = new ContentDatabase(JsonUtility.FromJson<ContentData>(File.ReadAllText(BalancePath)));
                Debug.Log("[ZooDefense] Balance JSON OK: " + db.Units.Count + " units, " + db.Enemies.Count + " enemies, " + db.Sends.Count + " sends.");
            }
            catch (System.Exception e) { Debug.LogError("[ZooDefense] Balance JSON invalid: " + e.Message); }
        }

        [MenuItem("Zoo Defense/Balance/Simulate 100 Bot Matches", priority = 22)]
        public static void SimulateBots()
        {
            var content = ContentLoader.Load();
            var waves = new List<int>();
            int[] wins = new int[3];
            var cmds = new List<Command>();
            for (int s = 0; s < 100; s++)
            {
                var m = new Match(content, new MatchSetup
                {
                    seed = 1000 + s,
                    players = new[]
                    {
                        new PlayerSetup { name = "A", deck = BotBrain.RandomDeck(content, s * 3 + 1), sendDeck = DefaultContent.DefaultSendDeck },
                        new PlayerSetup { name = "B", deck = BotBrain.RandomDeck(content, s * 7 + 2), sendDeck = DefaultContent.DefaultSendDeck },
                    },
                });
                var bots = new[] { new BotBrain(0, 1, s + 1), new BotBrain(1, 1, s + 2) };
                while (!m.IsOver && m.Tick < 20 * 60 * 30)
                {
                    cmds.Clear();
                    bots[0].Think(m, cmds);
                    bots[1].Think(m, cmds);
                    foreach (var c in cmds) m.Submit(c);
                    m.Step();
                }
                waves.Add(m.Wave);
                wins[m.Winner + 1]++;
            }
            waves.Sort();
            Debug.Log("[ZooDefense] 100 normal-bot matches: median end wave " + waves[50] + " (min " + waves[0] + ", max " + waves[99] +
                      "), P0 wins " + wins[1] + ", P1 wins " + wins[2] + ", draws " + wins[0] +
                      ". Target: median around wave 15-18 (5-6 minutes).");
        }

        [MenuItem("Zoo Defense/Art/Check Art", priority = 40)]
        public static void CheckArt()
        {
            var content = ContentLoader.Load();
            var sb = new StringBuilder();
            int found = 0, missing = 0;
            sb.AppendLine("Zoo Defense art check (missing art uses placeholders):");
            foreach (var u in content.Units)
            {
                bool has = Art.Unit(u.id, 1) != null;
                if (has) found++; else { missing++; sb.AppendLine("  missing  Units/" + u.id + "_1.png   (" + u.displayName + ")"); }
            }
            foreach (var e in content.Enemies)
            {
                bool has = Art.Enemy(e.id) != null;
                if (has) found++; else { missing++; sb.AppendLine("  missing  Enemies/" + e.id + ".png   (" + e.displayName + ")"); }
            }
            for (int i = 1; i <= MetaRules.Arenas.Length; i++)
                if (Art.Background(i) == null) { missing++; sb.AppendLine("  missing  Backgrounds/arena_" + i + ".png"); } else found++;
            foreach (var icon in new[] { "candy", "heart", "trophy", "gold" })
                if (Art.Icon(icon) == null) { missing++; sb.AppendLine("  missing  UI/" + icon + ".png"); } else found++;
            if (Resources.Load<TextAsset>("ZooArt/motion_profiles") == null) sb.AppendLine("  missing  ZooArt/motion_profiles.json (species motion; generic motion used)");
            sb.Insert(0, "Found " + found + ", missing " + missing + ". ");
            Art.ClearCache();
            Debug.Log(sb.ToString());
        }

        [MenuItem("Zoo Defense/Art/Open Art Folder", priority = 41)]
        public static void OpenArtFolder()
        {
            Directory.CreateDirectory(ArtRoot);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(ArtRoot + "/Units");
        }

        [MenuItem("Zoo Defense/Localization/Check Missing Translations", priority = 50)]
        public static void CheckTranslations()
        {
            foreach (var lang in Loc.Supported)
            {
                if (lang == Language.English) continue;
                var missing = Loc.MissingKeys(lang);
                if (missing.Count == 0) Debug.Log("[ZooDefense] " + lang + ": all " + "texts translated.");
                else Debug.LogWarning("[ZooDefense] " + lang + " is missing " + missing.Count + " key(s): " + string.Join(", ", missing.ToArray()));
            }
        }

        [MenuItem("Zoo Defense/Reset Local Profile", priority = 60)]
        public static void ResetProfile()
        {
            ProfileStore.Reset();
            Debug.Log("[ZooDefense] Local profile cleared.");
        }
    }

    /// <summary>On first open of the project: create the main scene, apply settings and open it.</summary>
    [InitializeOnLoad]
    static class FirstRunSetup
    {
        const string DoneKey = "ZooDefense.FirstRunDone";

        static FirstRunSetup()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            SessionState.SetBool(DoneKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ZooDefenseMenu.ScenePath)) return;
            ZooDefenseMenu.ApplyPlayerSettings();
            ZooDefenseMenu.CreateMainScene();
        }
    }

    /// <summary>Anything dropped into Resources/ZooArt is imported as a single, uncompressed-quality sprite.</summary>
    public sealed class ZooArtImporter : AssetPostprocessor
    {
        /// <summary>Music streams as Vorbis; short effects are decompressed on load (ADPCM, mono) for instant playback.</summary>
        void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/ZooAudio/")) return;
            var ai = (AudioImporter)assetImporter;
            var settings = ai.defaultSampleSettings;
            bool music = System.IO.Path.GetFileName(path).StartsWith("music_");
            if (music)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
            }
            else
            {
                ai.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            ai.defaultSampleSettings = settings;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/ZooArt/")) return;
            var ti = (TextureImporter)assetImporter;

            // Walk sheets (4x4 grid) are cut into frames at runtime: plain texture, no resizing to power of two.
            if (assetPath.Contains("/ZooArt/EnemyWalks/") || assetPath.Contains("/ZooArt/UnitCasts/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = assetPath.Contains("/Backgrounds/") ? 2048 : 512;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;

            // Character art: square canvas with the feet at 88% height -> pivot at the feet so motion
            // (bob, squash, tilt) happens around the ground contact point.
            if (assetPath.Contains("/ZooArt/Units/") || assetPath.Contains("/ZooArt/Enemies/"))
            {
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.12f);
                ti.SetTextureSettings(settings);
            }
        }
    }
}
