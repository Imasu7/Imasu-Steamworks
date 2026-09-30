using Imasu.Steamworks;
using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — PlayerListUI
    /// Builds a row per lobby member (Steam sessions) or per networked VoicePlayer
    /// (non-Steam LAN sessions). Your row prefab should carry a SteamUserBadge.
    ///
    /// How to use:
    ///   1. Make a row prefab (e.g. TextMeshPro name + SteamUserBadge) and assign
    ///      it to "rowPrefab".
    ///   2. Set "rowParent" to a VerticalLayoutGroup/ScrollRect content.
    ///   3. Call Refresh() (the template auto-refreshes every 0.5s).
    /// </summary>
    public class PlayerListUI : MonoBehaviour
    {
        [Tooltip("Row prefab that carries a SteamUserBadge (or name text).")]
        public GameObject rowPrefab;

        [Tooltip("Parent Transform rows are spawned under.")]
        public Transform rowParent;

        [Tooltip("Recreate rows this often while the panel is active.")]
        public float refreshInterval = 0.5f;

        readonly List<GameObject> rows = new List<GameObject>();
        float nextRefresh;

        void OnEnable()
        {
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        public void Refresh()
        {
            if (rowParent == null) return;
            foreach (var r in rows) Destroy(r.gameObject);
            rows.Clear();

            SteamLobbyManager lobby = SteamLobbyManager.instance;
            if (lobby != null && lobby.inLobby)
            {
                foreach (KeyValuePair<Steamworks.SteamId, string> kv in lobby.members)
                    SpawnRow(kv.Key.Value, kv.Value);
                return;
            }

#if VOICE
            // Non-Steam: list networked players (voice rows carry mic state).
            foreach (var vp in FindObjectsByType<VoicePlayer>(FindObjectsSortMode.None))
                SpawnRow(vp.OwnerSteamId, vp.GetDisplayName());
#endif
        }

        void SpawnRow(ulong steamId, string name)
        {
            if (rowPrefab == null) return;
            GameObject row = Instantiate(rowPrefab, rowParent);
            rows.Add(row);
            var badge = row.GetComponentInChildren<SteamUserBadge>();
            if (badge != null)
            {
                badge.SetUser(steamId, name);
                return;
            }
            var text = row.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = name;
        }
    }
}