using UnityEditor;

namespace Imasu.Steamworks.Editor
{
    /// <summary>
    /// Inspector for <see cref="ImasuSteamworksConfig"/> — the native grouped view with
    /// the gamemode/map catalogs removed (they have one canonical editor: the Control
    /// Center's Gamemodes &amp; Maps tab). Used both when the asset is selected in the
    /// Project window and by the Control Center's Advanced tab.
    /// </summary>
    [CustomEditor(typeof(ImasuSteamworksConfig))]
    public class ImasuSteamworksConfigEditor : UnityEditor.Editor
    {
        static readonly string[] HiddenProps = { "m_Script", "gameModes", "maps" };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Gamemodes & Maps are edited in the Control Center (Imasu → Steamworks Control Center, " +
                "Gamemodes & Maps tab). Everything here is a global default; gamemodes can override most fields.",
                MessageType.None);

            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (System.Array.IndexOf(HiddenProps, prop.name) >= 0) continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}