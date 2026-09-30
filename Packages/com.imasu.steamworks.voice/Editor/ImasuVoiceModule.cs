using UnityEditor;
using Imasu.Steamworks.Editor.Addons;

namespace Imasu.Steamworks.Editor
{
    /// <summary>Registers the Voice addon's settings in the Control Center Modules tab.</summary>
    public class ImasuVoiceModule : IImasuModuleEditor
    {
        public string ModuleId => "com.imasu.steamworks.voice";
        public string DisplayName => "Voice";

        public void DrawSettings()
        {
            EditorGUILayout.HelpBox(
                "Distance VOIP via the Steam voice codec (record → compress → unreliable channel → " +
                "per-player 3D AudioSource). Requires Steam — LAN sessions have no voice.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Defaults: VOIP enabled, Range attenuation (min/max rolloff), per-user volume. " +
                "Override per gamemode in the Gamemodes tab → Voice section.",
                MessageType.None);
        }
    }
}