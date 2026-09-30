using Imasu.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — InviteButton
    /// Sends a Steam lobby invite for a friend, or opens the native overlay
    /// picker when no specific friend is set.
    ///
    /// How to use:
    ///   1. Add this to a Button GameObject with a TMP_Text child (or set
    ///      feedbackText manually).
    ///   2. Set friendSteamId to a specific friend, or leave 0 to open the
    ///      Steam overlay picker.
    ///   3. Done. Green "Invited" / amber "Retry" feedback is automatic.
    /// </summary>
    public class InviteButton : MonoBehaviour
    {
        [Tooltip("Friend to invite. 0 = open the native Steam overlay picker.")]
        public ulong friendSteamId;

        [Tooltip("Optional text that shows 'Invited' / 'Retry'.")]
        public TextMeshProUGUI feedbackText;

        [Tooltip("Optional image tinted green/amber to show the result.")]
        public Image feedbackImage;

        void OnEnable()
        {
            var button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Invite);
        }

        void OnDisable()
        {
            var button = GetComponent<Button>();
            if (button != null) button.onClick.RemoveListener(Invite);
        }

        public void Invite()
        {
            SteamLobbyManager lobby = SteamLobbyManager.instance;
            if (lobby == null || !lobby.inLobby) return;

            if (friendSteamId == 0)
            {
                SteamFriends.OpenGameInviteOverlay(lobby.currentLobby.Id);
                return;
            }

            bool invited = false;
            try { invited = lobby.currentLobby.InviteFriend((Steamworks.SteamId)friendSteamId); }
            catch { invited = false; }

            if (feedbackText != null) feedbackText.text = invited ? "Invited" : "Retry";
            if (feedbackImage != null)
                feedbackImage.color = invited
                    ? new Color(0.25f, 0.6f, 0.3f, 1f)
                    : new Color(0.9f, 0.6f, 0.15f, 1f);
        }
    }
}