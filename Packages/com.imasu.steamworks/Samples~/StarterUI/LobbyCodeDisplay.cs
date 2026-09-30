using Imasu.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — LobbyCodeDisplay
    /// Shows the current lobby's short code in a read-only box and copies it to
    /// the clipboard when clicked (flash "Copied!" for 1.2s).
    ///
    /// How to use:
    ///   1. Add this to a UI GameObject with a Button + TextMeshPro (or provide
    ///      the TMP_Text slot on any object).
    ///   2. Drag the TMP_Text into "codeText".
    ///   3. Done — it reads SteamLobbyManager automatically.
    /// </summary>
    public class LobbyCodeDisplay : MonoBehaviour
    {
        [Tooltip("The read-only text element that shows the code.")]
        public TextMeshProUGUI codeText;

        [Tooltip("Optional status line for 'Copied!' feedback.")]
        public TextMeshProUGUI statusText;

        float lastCopyTime = float.MinValue;

        void Update()
        {
            SteamLobbyManager lobby = SteamLobbyManager.instance;
            if (lobby == null || codeText == null) return;

            bool justCopied = Time.unscaledTime - lastCopyTime < 1.2f;
            string code = lobby.settings.code;
            if (string.IsNullOrEmpty(code) && lobby.currentLobby.Id != default)
                code = lobby.currentLobby.Id.ToString();

            codeText.text = justCopied ? "Copied!" : (string.IsNullOrEmpty(code) ? "-" : code);
        }

        public void Copy()
        {
            if (codeText == null) return;
            GUIUtility.systemCopyBuffer = codeText.text;
            lastCopyTime = Time.unscaledTime;
        }
    }
}