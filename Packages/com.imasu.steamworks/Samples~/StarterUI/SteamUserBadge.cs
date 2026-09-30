using Imasu.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — SteamUserBadge
    /// Shows a player's Steam avatar + name on a UI row.
    ///
    /// How to use:
    ///   1. Add this to a row GameObject that has a RawImage (avatar) and a
    ///      TextMeshPro (name).
    ///   2. Drag the RawImage into "avatar", the TMP_Text into "nameText".
    ///   3. Set steamId at runtime (e.g. from SteamLobbyManager.members keys)
    ///      or in the inspector for a fixed player.
    ///
    /// The avatar downloads lazily (friends' avatars aren't cached locally) and
    /// fills in automatically when it arrives.
    /// </summary>
    public class SteamUserBadge : MonoBehaviour
    {
        [Tooltip("UI element that will show the Steam avatar.")]
        public RawImage avatar;

        [Tooltip("UI element that will show the player name.")]
        public TextMeshProUGUI nameText;

        [Tooltip("Steam ID to display. Leave 0 to set at runtime.")]
        public ulong steamId;

        [Tooltip("Fallback name shown before the persona name loads.")]
        public string fallbackName = "Player";

        void OnEnable()
        {
            Refresh();
        }

        /// <summary>Point the badge at a specific Steam user and refresh.</summary>
        public void SetUser(ulong id, string name)
        {
            steamId = id;
            fallbackName = string.IsNullOrEmpty(name) ? fallbackName : name;
            Refresh();
        }

        public void Refresh()
        {
            if (nameText != null) nameText.text = fallbackName;
            if (avatar == null || steamId == 0) return;

            // Sets the texture immediately if cached, otherwise late-assigns
            // the moment the download finishes (survives row rebuilds).
            var late = avatar.GetComponent<AvatarCache.AvatarLateAssign>();
            if (late == null) late = avatar.gameObject.AddComponent<AvatarCache.AvatarLateAssign>();
            late.Init(steamId, avatar);

            // Ask Steam for the persona name too.
            try
            {
                var friend = new Steamworks.Friend((Steamworks.SteamId)steamId);
                if (!string.IsNullOrEmpty(friend.Name)) nameText.text = friend.Name;
            }
            catch { /* Steam off — keep fallback */ }
        }
    }
}