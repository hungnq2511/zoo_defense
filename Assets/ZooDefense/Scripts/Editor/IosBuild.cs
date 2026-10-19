using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace ZooDefense.EditorTools
{
    /// <summary>
    /// iOS / TestFlight build: settings from BuildConfig/ios.json, Xcode project export, Info.plist fixes and the
    /// ExportOptions.plist that Tools/testflight.sh uses to archive and upload.
    ///   Menu:  Zoo Defense/iOS/...
    ///   CLI:   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod ZooDefense.EditorTools.IosBuild.BuildFromCommandLine
    /// </summary>
    public static class IosBuild
    {
        public const string ConfigPath = "BuildConfig/ios.json";
        public const string OutputPath = "Builds/iOS";

        [Serializable]
        public class Config
        {
            public string bundleId = "site.nutrica.zoodefense";
            public string teamId = "";            // Apple Developer Team ID (10 characters), developer.apple.com > Membership
            public string displayName = "Zoo Defense";
            public string companyName = "Nutrica";
            public string version = "0.1.0";      // CFBundleShortVersionString (shown in TestFlight)
            public string minIosVersion = "15.0";
        }

        public static Config LoadConfig()
        {
            var c = new Config();
            if (File.Exists(ConfigPath))
            {
                try { JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigPath), c); }
                catch (Exception e) { Debug.LogError("[iOS] " + ConfigPath + " is invalid: " + e.Message); }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(c, true));
                Debug.Log("[iOS] Created " + ConfigPath + ". Fill in teamId (and bundleId if you registered another one).");
            }
            // Allow CI / the shell script to override.
            var envTeam = Environment.GetEnvironmentVariable("ZOO_TEAM_ID");
            if (!string.IsNullOrEmpty(envTeam)) c.teamId = envTeam;
            var envBundle = Environment.GetEnvironmentVariable("ZOO_BUNDLE_ID");
            if (!string.IsNullOrEmpty(envBundle)) c.bundleId = envBundle;
            return c;
        }

        [MenuItem("Zoo Defense/iOS/Apply TestFlight Settings", priority = 40)]
        public static void ApplySettings() { Apply(LoadConfig(), false); }

        static void Apply(Config c, bool bumpBuild)
        {
            var ios = NamedBuildTarget.iOS;
            ZooDefenseMenu.ApplyPlayerSettings();
            PlayerSettings.companyName = c.companyName;
            PlayerSettings.productName = c.displayName;
            PlayerSettings.SetApplicationIdentifier(ios, c.bundleId);
            PlayerSettings.bundleVersion = c.version;
            if (bumpBuild || string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = DateTime.UtcNow.ToString("yyMMddHHmm");   // always increasing: TestFlight rejects reused numbers

            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(ios, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(ios, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.iOS.targetOSVersionString = c.minIosVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            if (!string.IsNullOrEmpty(c.teamId)) PlayerSettings.iOS.appleDeveloperTeamID = c.teamId;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Scene list
            if (!EditorBuildSettings.scenes.Any(s => s.path == ZooDefenseMenu.ScenePath) && File.Exists(ZooDefenseMenu.ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ZooDefenseMenu.ScenePath, true) };

            AssetDatabase.SaveAssets();
            Debug.Log("[iOS] Settings applied: " + c.bundleId + " v" + c.version + " (" + PlayerSettings.iOS.buildNumber + "), team " + (string.IsNullOrEmpty(c.teamId) ? "<missing>" : c.teamId));
        }

        [MenuItem("Zoo Defense/iOS/Build Xcode Project", priority = 41)]
        public static void BuildFromMenu()
        {
            if (!Build(true)) EditorUtility.DisplayDialog("Zoo Defense", "iOS build failed. See the Console.", "OK");
            else EditorUtility.RevealInFinder(OutputPath);
        }

        /// <summary>Entry point for Tools/testflight.sh (batch mode). Exits with code 1 on failure.</summary>
        public static void BuildFromCommandLine()
        {
            bool ok = Build(true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(bool bumpBuild)
        {
            var c = LoadConfig();
            if (string.IsNullOrEmpty(c.teamId)) Debug.LogWarning("[iOS] teamId is empty in " + ConfigPath + ": signing will fail in Xcode until it is set.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                {
                    Debug.LogError("[iOS] iOS Build Support is not installed. Unity Hub > Installs > 6000.0.58f2 > Add modules > iOS Build Support.");
                    return false;
                }
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }
            Apply(c, bumpBuild);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };
            if (options.scenes.Length == 0) { Debug.LogError("[iOS] No scene: run Zoo Defense/Create Main Scene first."); return false; }
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[iOS] Build " + s.result + " in " + s.totalTime + " -> " + Path.GetFullPath(OutputPath));
            return s.result == BuildResult.Succeeded;
        }

        /// <summary>Info.plist / Xcode fixes App Store Connect asks for, plus ExportOptions.plist for the upload.</summary>
        [PostProcessBuild(100)]
        public static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var c = LoadConfig();
#if UNITY_IOS
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            // Only standard HTTPS/TLS is used: exempt from export compliance, so no question on every upload.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            // Shown in the iOS tracking permission dialog (asked before the ad SDK starts).
            root.SetString("NSUserTrackingUsageDescription", "Zoo Defense uses this to show you more relevant ads, which keeps the game free.");
            root.SetString("CFBundleDisplayName", c.displayName);
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            if (root["UIApplicationExitsOnSuspend"] != null) root.values.Remove("UIApplicationExitsOnSuspend");   // deprecated key, rejected by validation
            plist.WriteToFile(plistPath);

            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            var main = proj.GetUnityMainTargetGuid();
            var fw = proj.GetUnityFrameworkTargetGuid();
            foreach (var t in new[] { main, fw })
            {
                proj.SetBuildProperty(t, "ENABLE_BITCODE", "NO");
                if (!string.IsNullOrEmpty(c.teamId)) proj.SetBuildProperty(t, "DEVELOPMENT_TEAM", c.teamId);
            }
            proj.SetBuildProperty(main, "CODE_SIGN_STYLE", "Automatic");
            proj.WriteToFile(projPath);
#endif
            File.WriteAllText(Path.Combine(path, "ExportOptions.plist"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\"><dict>\n" +
                "  <key>method</key><string>app-store-connect</string>\n" +
                "  <key>destination</key><string>upload</string>\n" +
                "  <key>signingStyle</key><string>automatic</string>\n" +
                "  <key>teamID</key><string>" + c.teamId + "</string>\n" +
                "  <key>uploadSymbols</key><true/>\n" +
                "  <key>manageAppVersionAndBuildNumber</key><false/>\n" +
                "</dict></plist>\n");
        }
    }
}
