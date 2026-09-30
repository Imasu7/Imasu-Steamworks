using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Imasu.Steamworks.Editor.Dedicated
{
    /// <summary>
    /// Builds a Linux headless dedicated server for Imasu Steamworks:
    /// 1. Copies the Active Config into Assets/Resources/Config so it ships in the build
    ///    (DedicatedHost loads it at runtime under the same path).
    /// 2. Builds every scene in the classic Build Settings list (menu + lobby + maps —
    ///    keep it in sync via the Control Center's "Sync Build Settings").
    /// 3. The server auto-starts when run with the -imasuDedicated flag.
    /// </summary>
    public static class DedicatedBuildHelper
    {
        const string ActiveConfigPref = "ImasuSteamworks.ActiveConfigPath";
        const string ResourcesConfigDir = "Assets/Resources/Config";
        const string ResourcesConfigPath = ResourcesConfigDir + "/ImasuSteamworksConfig.asset";

        [MenuItem("Imasu/Steamworks/Dedicated/Build Linux Headless Server…")]
        public static void BuildLinuxHeadlessServerMenu()
        {
            string output = EditorUtility.SaveFolderPanel("Linux Headless Server Build", "Builds", "DedicatedServer");
            if (string.IsNullOrEmpty(output)) return;
            BuildLinuxHeadlessServer(output);
        }

        public static bool BuildLinuxHeadlessServer(string outputDir)
        {
            ImasuSteamworksConfig cfg = LoadActiveConfig();
            if (cfg == null)
            {
                EditorUtility.DisplayDialog("Dedicated Build", "No Active Config found — create one in the Control Center first.", "OK");
                return false;
            }
            SyncConfigToResources(cfg);

            string exe = Path.Combine(outputDir, "DedicatedServer.x86_64");
            var options = new BuildPlayerOptions
            {
                scenes = GatherScenes().ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[Dedicated] Linux headless server build {(ok ? "SUCCEEDED" : "FAILED")} -> {exe}");
            if (!ok)
                Debug.LogError($"[Dedicated] Build report:\n{report}");
            return ok;
        }

        /// <summary>Scene list for the build: the active Unity 6 Build Profile when it
        /// has scenes, else the classic EditorBuildSettings list. Kept additive so the
        /// Control Center's "Sync Build Settings" is honoured either way.</summary>
        static List<string> GatherScenes()
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
#if UNITY_6000_0_OR_NEWER
            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (profile != null && profile.scenes != null && profile.scenes.Length > 0)
            {
                foreach (var s in profile.scenes)
                    if (s.enabled && seen.Add(s.path)) list.Add(s.path);
                return list;
            }
#endif
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && seen.Add(s.path)) list.Add(s.path);
            return list;
        }

        static ImasuSteamworksConfig LoadActiveConfig()
        {
            string pref = EditorPrefs.GetString(ActiveConfigPref, "");
            if (!string.IsNullOrEmpty(pref))
            {
                var c = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(pref);
                if (c != null) return c;
            }
            string[] guids = AssetDatabase.FindAssets("t:ImasuSteamworksConfig");
            foreach (string g in guids)
            {
                var c = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>Copies the Active Config into Assets/Resources/Config so the build
        /// includes it (Resources assets always ship). DedicatedHost loads it at runtime.</summary>
        static void SyncConfigToResources(ImasuSteamworksConfig cfg)
        {
            if (!AssetDatabase.IsValidFolder(ResourcesConfigDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(cfg), ResourcesConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dedicated] Config '{cfg.name}' copied to {ResourcesConfigPath}");
        }
    }
}