using UnityEditor;
using UnityEngine;
using Imasu.Steamworks.Editor.Addons;

namespace Imasu.Steamworks.Editor
{
    /// <summary>Registers the Dedicated Servers addon in the Control Center Modules tab.</summary>
    public class ImasuDedicatedModule : IImasuModuleEditor
    {
        public string ModuleId => "com.imasu.steamworks.dedicated";
        public string DisplayName => "Dedicated Servers";

        public void DrawSettings()
        {
            EditorGUILayout.HelpBox(
                "Headless dedicated-server hosting (KCP, no Steam session required). Depends on core only — " +
                "a separate addon from LAN.",
                MessageType.Info);
            if (GUILayout.Button("Build Linux Headless Server…", GUILayout.Height(24)))
                Dedicated.DedicatedBuildHelper.BuildLinuxHeadlessServerMenu();
            EditorGUILayout.HelpBox(
                "Run the server with the -imasuDedicated flag to auto-start. Make sure your scenes are in " +
                "Build Settings (Control Center → Project Scenes & Lobby → Sync Build Settings) so the build includes them.",
                MessageType.None);
        }
    }
}