using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ZooDefense.EditorTools
{
    /// <summary>
    /// Turns the real ads / store code on when its package is installed: sets ZOO_LEVELPLAY when the Ads Mediation
    /// package (LevelPlay) is present and ZOO_IAP when In-App Purchasing is present. Without them the game keeps the
    /// test ads and the free test store, so the project always compiles.
    /// </summary>
    [InitializeOnLoad]
    public static class MonetizationDefines
    {
        static readonly NamedBuildTarget[] Targets = { NamedBuildTarget.iOS, NamedBuildTarget.Android, NamedBuildTarget.Standalone };

        static MonetizationDefines()
        {
            EditorApplication.delayCall += Refresh;
        }

        [MenuItem("Zoo Defense/Monetization/Refresh Ads + Store Defines", priority = 60)]
        public static void Refresh()
        {
            bool levelPlay = TypeExists("Unity.Services.LevelPlay.LevelPlay");
            bool iap = TypeExists("UnityEngine.Purchasing.UnityIAPServices");
            foreach (var t in Targets)
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(t).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var before = string.Join(";", defines);
                Set(defines, "ZOO_LEVELPLAY", levelPlay);
                Set(defines, "ZOO_IAP", iap);
                var after = string.Join(";", defines);
                if (after != before)
                {
                    PlayerSettings.SetScriptingDefineSymbols(t, after);
                    Debug.Log("[Monetization] " + t.TargetName + " defines: " + after);
                }
            }
        }

        static void Set(List<string> defines, string symbol, bool on)
        {
            if (on && !defines.Contains(symbol)) defines.Add(symbol);
            if (!on) defines.Remove(symbol);
        }

        static bool TypeExists(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (asm.GetType(fullName, false) != null) return true; }
                catch (Exception) { }
            }
            return false;
        }
    }
}
