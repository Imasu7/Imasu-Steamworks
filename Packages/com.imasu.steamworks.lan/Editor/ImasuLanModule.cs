using UnityEditor;
using UnityEngine;
using Imasu.Steamworks.Editor.Addons;

namespace Imasu.Steamworks.Editor
{
    /// <summary>Registers the LAN addon's settings in the Control Center Modules tab.</summary>
    public class ImasuLanModule : IImasuModuleEditor
    {
        public string ModuleId => "com.imasu.steamworks.lan";
        public string DisplayName => "LAN (no Steam)";

        const string PrefPort = "imasu_lan_port";
        const string PrefActive = "imasu_lan_active";
        const string PrefMaxStale = "imasu_lan_max_stale";

        public void DrawSettings()
        {
            EditorGUILayout.HelpBox(
                "UDP LAN discovery without Steam. LAN hosts appear in the server browser when " +
                "'Allow non-Steam servers' is enabled in the config. Voice on LAN is unavailable " +
                "(voice uses Steam APIs).",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            int port = EditorGUILayout.IntField(
                new GUIContent("Discovery Port",
                    "UDP port used to listen for and broadcast discovery messages."),
                PlayerPrefs.GetInt(PrefPort, 47777));
            bool active = EditorGUILayout.Toggle(
                new GUIContent("Active Discovery",
                    "Periodically broadcast a probe to find LAN hosts. Off = passive listen only."),
                PlayerPrefs.GetInt(PrefActive, 1) != 0);
            float maxStale = EditorGUILayout.Slider(
                new GUIContent("Max Stale (s)",
                    "How long a discovered server stays listed without a reply."),
                PlayerPrefs.GetFloat(PrefMaxStale, 7f), 1f, 60f);
            if (EditorGUI.EndChangeCheck())
            {
                PlayerPrefs.SetInt(PrefPort, Mathf.Max(1024, port));
                PlayerPrefs.SetInt(PrefActive, active ? 1 : 0);
                PlayerPrefs.SetFloat(PrefMaxStale, maxStale);
                PlayerPrefs.Save();
                LanDiscovery.Instance?.ApplySettings();
            }

            EditorGUILayout.HelpBox(
                "Settings persist per-project (PlayerPrefs) and apply the next time the browser " +
                "opens or a host advertises.",
                MessageType.None);
        }
    }
}