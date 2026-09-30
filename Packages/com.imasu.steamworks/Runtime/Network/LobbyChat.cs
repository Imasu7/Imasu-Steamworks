using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Lobby / waiting-room text chat. Server-authoritative: clients send a
    /// <c>[Command]</c>, the server relays a <c>[ClientRpc]</c> so every client
    /// (including the host's own local client) appends the same entry. Lives on a
    /// scene object in the online (WaitingRoom) scene so it exists for host and
    /// joiners without any spawned player. Works for Steam and LAN (KCP) sessions.
    /// </summary>
    public class LobbyChat : NetworkBehaviour
    {
        public static LobbyChat instance { get; private set; }

        [Header("Limits")]
        [Tooltip("Max messages kept + shown before the oldest are dropped.")]
        public int maxEntries = 50;

        [Tooltip("Max characters per message.")]
        public int maxLength = 200;

        [Tooltip("Min seconds between messages per connection (server-side).")]
        public float rateLimitSeconds = 0.5f;

        /// <summary>All messages this client knows about, newest last.</summary>
        public readonly List<ChatEntry> entries = new List<ChatEntry>();

        /// <summary>Fired whenever a new entry is appended (any client).</summary>
        public event Action onEntriesChanged;

        readonly Dictionary<int, float> lastSendByConn = new Dictionary<int, float>();

        void OnEnable()
        {
            if (instance == null) instance = this;
        }

        void OnDisable()
        {
            if (instance == this) instance = null;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>The local player's display name (Steam name, or "Player" on LAN).</summary>
        public string OwnDisplayName()
        {
            if (SteamClient.IsValid)
            {
                try
                {
                    SteamId id = SteamClient.SteamId;
                    if (SteamLobbyManager.instance != null &&
                        SteamLobbyManager.instance.members.TryGetValue(id, out string n) &&
                        !string.IsNullOrEmpty(n))
                        return n;
                    return new Friend(id).Name;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyChat] Steam name lookup failed: {e.Message}");
                }
            }
            return "Player";
        }

        /// <summary>Client side: send your own line. The host short-circuits to the
        /// server path (no round-trip through its own network stack needed).</summary>
        public void Send(string text)
        {
            if (!NetworkClient.active) return;
            string clean = Sanitize(text, maxLength);
            if (clean.Length == 0) return;

            if (NetworkServer.active && NetworkClient.active)
            {
                ServerReceive(OwnDisplayName(), clean);
                return;
            }
            CmdSay(OwnDisplayName(), clean);
        }

        /// <summary>Server side: broadcast a system line (e.g. host announcements).</summary>
        [Server]
        public void ServerAnnounce(string text)
        {
            string clean = Sanitize(text, maxLength);
            if (clean.Length == 0) return;
            RpcChat(0, "", clean, true);
        }

        [Command(requiresAuthority = false)]
        void CmdSay(string name, string text)
        {
            if (!isServer) return;
            if (text.Length == 0) return;

            if (connectionToClient != null)
            {
                int conn = connectionToClient.connectionId;
                float now = Time.time;
                if (lastSendByConn.TryGetValue(conn, out float last) && now - last < rateLimitSeconds)
                    return;
                lastSendByConn[conn] = now;
            }
            RpcChat(0, Sanitize(name, maxLength), text, false);
        }

        void ServerReceive(string name, string text)
        {
            if (!isServer) return;
            RpcChat(0, Sanitize(name, maxLength), text, false);
        }

        [ClientRpc]
        void RpcChat(ulong steamId, string name, string text, bool system)
        {
            Add(new ChatEntry { steamId = steamId, name = name, text = text, isSystem = system });
        }

        void Add(ChatEntry entry)
        {
            entries.Add(entry);
            if (entries.Count > maxEntries)
                entries.RemoveRange(0, entries.Count - maxEntries);
            onEntriesChanged?.Invoke();
        }

        static string Sanitize(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            if (s.Length > maxLength) s = s.Substring(0, maxLength);
            return s.Replace("\r", " ").Replace("\n", " ");
        }
    }

    /// <summary>One chat line as relayed to clients.</summary>
    [Serializable]
    public struct ChatEntry
    {
        public ulong steamId; // 0 for system / LAN
        public string name;
        public string text;
        public bool isSystem;
    }
}