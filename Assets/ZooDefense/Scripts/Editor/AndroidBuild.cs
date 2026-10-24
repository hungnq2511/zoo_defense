using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZooDefense.EditorTools
{
    /// <summary>
    /// Android / Google Play build: settings from BuildConfig/android.json, a signed .aab (Android App Bundle) in
    /// Builds/Android. Signing passwords come from environment variables (never saved in the project):
    ///   ZOO_KEYSTORE_PATH, ZOO_KEYSTORE_PASS, ZOO_KEY_ALIAS, ZOO_KEY_PASS — Tools/android_build.sh sets them from
    ///   BuildConfig/android_signing/keystore.properties (created on the first run, kept out of git).
    ///   Menu:  Zoo Defense/Android/...
    ///   CLI:   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod ZooDefense.EditorTools.AndroidBuild.BuildFromCommandLine
    /// </summary>
    public static class AndroidBuild
    {
        public const string ConfigPath = "BuildConfig/android.json";
        public const string OutputDir = "Builds/Android";

        [Serializable]
        public class Config
        {
            public string packageName = "com.hungnq.zoodefense";
            public string displayName = "Zoo Defense";
            public string companyName = "Nutrica";
            public string version = "1.0";          // versionName shown on Google Play
            public int minSdk = 24;                  // Android 7.0
            public bool appBundle = true;            // Google Play needs .aab; false = .apk for a quick install test
        }

        public static Config LoadConfig()
        {
            var c = new Config();
            if (File.Exists(ConfigPath))
            {
                try { JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigPath), c); }
                catch (Exception e) { Debug.LogError("[Android] " + ConfigPath + " is invalid: " + e.Message); }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(c, true));
            }
            return c;
        }

        /// <summary>Minutes since 2025-01-01: always increasing (Google Play rejects a reused versionCode) and far below the 2,100,000,000 cap.</summary>
        static int NextVersionCode()
        {
            int code = (int)(DateTime.UtcNow - new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMinutes;
            return Math.Max(code, PlayerSettings.Android.bundleVersionCode + 1);
        }

        [MenuItem("Zoo Defense/Android/Apply Google Play Settings", priority = 50)]
        public static void ApplySettings() { Apply(LoadConfig(), false); }

        static void Apply(Config c, bool bumpBuild)
        {
            var android = NamedBuildTarget.Android;
            ZooDefenseMenu.ApplyPlayerSettings();
            PlayerSettings.companyName = c.companyName;
            PlayerSettings.productName = c.displayName;
            PlayerSettings.SetApplicationIdentifier(android, c.packageName);
            PlayerSettings.bundleVersion = c.version;
            if (bumpBuild) PlayerSettings.Android.bundleVersionCode = NextVersionCode();

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            // Google Play needs 64-bit; ARMv7 keeps older 32-bit phones.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)Mathf.Max(c.minSdk, 23);
            // Highest SDK installed with Unity: Google Play requires a recent target API every year.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.minifyRelease = false;          // ad / billing SDKs break when R8 strips them
            EditorUserBuildSettings.buildAppBundle = c.appBundle;
            // Native symbols: Google Play shows readable crash stacks with them.
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            if (!EditorBuildSettings.scenes.Any(s => s.path == ZooDefenseMenu.ScenePath) && File.Exists(ZooDefenseMenu.ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ZooDefenseMenu.ScenePath, true) };

            AssetDatabase.SaveAssets();
            Debug.Log("[Android] Settings applied: " + c.packageName + " v" + c.version + " (" + PlayerSettings.Android.bundleVersionCode + ")");
        }

        /// <summary>Signing from environment variables; false (with an error) when a release key is missing.</summary>
        static bool ApplySigning()
        {
            string path = Environment.GetEnvironmentVariable("ZOO_KEYSTORE_PATH");
            string pass = Environment.GetEnvironmentVariable("ZOO_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("ZOO_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("ZOO_KEY_PASS");
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(alias))
            {
                Debug.LogError("[Android] No upload key: run ./Tools/android_build.sh (it creates the key on the first run), or set ZOO_KEYSTORE_PATH / ZOO_KEYSTORE_PASS / ZOO_KEY_ALIAS / ZOO_KEY_PASS.");
                return false;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(path);
            PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? pass : keyPass;
            return true;
        }

        /// <summary>LevelPlay pulls its Android libraries through the External Dependency Manager: resolve them before a batch build.</summary>
        static void ResolveDependencies()
        {
            var resolver = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("GooglePlayServices.PlayServicesResolver", false); } catch { return null; } })
                .FirstOrDefault(t => t != null);
            if (resolver == null) { Debug.Log("[Android] External Dependency Manager not found: skipping dependency resolution."); return; }
            var sync = resolver.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            if (sync == null) { Debug.LogWarning("[Android] PlayServicesResolver.ResolveSync not found."); return; }
            try
            {
                object ok = sync.Invoke(null, new object[] { true });
                Debug.Log("[Android] Android dependencies resolved: " + ok);
            }
            catch (Exception e) { Debug.LogWarning("[Android] Dependency resolution failed: " + (e.InnerException ?? e).Message); }
        }

        [MenuItem("Zoo Defense/Android/Build App Bundle (.aab)", priority = 51)]
        public static void BuildFromMenu()
        {
            string file;
            if (!Build(true, out file)) EditorUtility.DisplayDialog("Zoo Defense", "Android build failed. See the Console.", "OK");
            else EditorUtility.RevealInFinder(file);
        }

        /// <summary>Entry point for Tools/android_build.sh (batch mode). Exits with code 1 on failure.</summary>
        public static void BuildFromCommandLine()
        {
            string file;
            bool ok = Build(true, out file);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(bool bumpBuild, out string file)
        {
            file = null;
            var c = LoadConfig();
            // Application.cloudProjectId = the id baked into the build (ProjectSettings cloudProjectId); CloudProjectSettings.projectId
            // is empty in batch mode when the Editor is not signed in, so it is not used here.
            // Without the Unity Cloud project link the build cannot reach Unity Services: no online matches,
            // no purchase check (stuck on "verifying"), no account link. Stop instead of shipping that.
            if (string.IsNullOrEmpty(Application.cloudProjectId) && !System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText("ProjectSettings/ProjectSettings.asset"), @"cloudProjectId: [0-9a-f-]{36}"))
            {
                Debug.LogError("[Android] This Unity project is not linked to Unity Cloud (Project Settings > Services). Link it to the project \"Zoo Defense\" first.");
                return false;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Android] Android Build Support is not installed. Unity Hub > Installs > 6000.0.58f2 > Add modules > Android Build Support (with OpenJDK and Android SDK & NDK Tools).");
                return false;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            MonetizationDefines.Refresh();
            Apply(c, bumpBuild);
            if (!ApplySigning()) return false;
            ResolveDependencies();

            Directory.CreateDirectory(OutputDir);
            file = Path.Combine(OutputDir, "ZooDefense-" + c.version + "-" + PlayerSettings.Android.bundleVersionCode + (c.appBundle ? ".aab" : ".apk"));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = file,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Android] Build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
                return false;
            }
            Debug.Log("[Android] Built " + file + " (" + (report.summary.totalSize / 1048576f).ToString("0.0") + " MB, versionCode " + PlayerSettings.Android.bundleVersionCode + ")");
            File.WriteAllText(Path.Combine(OutputDir, "last_build.txt"), file);
            return true;
        }
    }
}
