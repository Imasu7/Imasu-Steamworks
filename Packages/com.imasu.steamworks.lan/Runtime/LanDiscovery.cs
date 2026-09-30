using Mirror;
using Mirror.Discovery;
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>Discovery request — nothing to ask for, presence ping only.</summary>
    public struct LanRequest : NetworkMessage { }

    /// <summary>Broadcast reply from a LAN host advertising its game.</summary>
    public struct LanResponse : NetworkMessage
    {
        public long serverId;
        public string uri;          // kcp://host:port (host rewritten on receive)
        public string serverName;
        public int currentPlayers;
        public int maxPlayers;
        public string mapName;
    }

    /// <summary>A discovered LAN server the browser can join by URI.</summary>
    public class LanServer
    {
        public long serverId;
        public string uri;
        public string serverName;
        public int currentPlayers;
        public int maxPlayers;
        public string mapName;
    }

    /// <summary>
    /// UDP LAN discovery (Mirror's NetworkDiscoveryBase): hosts advertise on the
    /// local subnet, the browser collects replies and lists them alongside Steam
    /// lobbies. Works entirely without Steam — the KCP transport handles the
    /// actual game traffic once joined.
    ///
    /// Mirror's callback may arrive on a worker thread, so replies are stashed in
    /// a lock-free-ish list that the UI polls on the main thread (see found).
    /// </summary>
    public class LanDiscovery : NetworkDiscoveryBase<LanRequest, LanResponse>, ILanDiscovery
    {
        public static LanDiscovery Instance { get; private set; }

        public readonly List<LanServer> found = new List<LanServer>();
        readonly object sync = new object();

        // Tunables, persisted via PlayerPrefs so the Control Center LAN tab can
        // adjust them without core knowing about the addon. Defaults mirror
        // Mirror's NetworkDiscoveryBase.
        const string PrefPort = "imasu_lan_port";
        const string PrefActive = "imasu_lan_active";
        const string PrefMaxStale = "imasu_lan_max_stale";

        [Tooltip("UDP port used to listen for and broadcast discovery messages.")]
        public int port = 47777;

        [Tooltip("Periodically broadcast a probe to find LAN hosts. Off = passive listen only.")]
        public bool activeDiscovery = true;

        [Tooltip("How long a discovered server stays listed without a reply (seconds).")]
        public float maxStaleSeconds = 7f;

        struct SeenEntry { public LanServer server; public float lastSeen; }
        readonly Dictionary<long, SeenEntry> seen = new Dictionary<long, SeenEntry>();

        public static void Ensure()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("LanDiscovery");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<LanDiscovery>();
        }

        // The LAN addon auto-initializes so core (ImasuNetworkManager /
        // ImasuSessionUI) can find it via ILanDiscovery with no hard reference.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoEnsure()
        {
            Ensure();
        }

        public override void Start()
        {
            LoadSettings();
            base.Start();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Applies persisted settings over the defaults. Called at Start and
        /// from the Control Center LAN tab; takes effect the next time discovery
        /// starts or a server advertises.</summary>
        public void ApplySettings()
        {
            serverBroadcastListenPort = Mathf.Max(1024, port);
            enableActiveDiscovery = activeDiscovery;
            maxStaleSeconds = Mathf.Clamp(maxStaleSeconds, 1f, 60f);
        }

        void LoadSettings()
        {
            if (PlayerPrefs.HasKey(PrefPort)) port = PlayerPrefs.GetInt(PrefPort, 47777);
            if (PlayerPrefs.HasKey(PrefActive)) activeDiscovery = PlayerPrefs.GetInt(PrefActive, 1) != 0;
            if (PlayerPrefs.HasKey(PrefMaxStale)) maxStaleSeconds = PlayerPrefs.GetFloat(PrefMaxStale, 7f);
            ApplySettings();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopDiscovery();
        }

        // ------------------------------------------------------------------ //
        //  Server (host) side
        // ------------------------------------------------------------------ //

        protected override LanResponse ProcessRequest(LanRequest request, IPEndPoint endpoint)
        {
            ImasuNetworkManager nm = NetworkManager.singleton as ImasuNetworkManager;
            if (nm == null || !NetworkServer.active || transport == null)
                return default; // not advertising a live game right now

            try
            {
                string uri = transport.ServerUri().ToString();
                string name = "LAN Game";
                int maxPlayers = nm.maxConnections;
                string map = nm.matchScene;

                SteamLobbyManager lobby = SteamLobbyManager.instance;
                if (lobby != null && lobby.inLobby)
                {
                    name = lobby.settings.lobbyName;
                    if (lobby.settings.maxPlayers > 0) maxPlayers = lobby.settings.maxPlayers;
                    map = lobby.settings.sceneName;
                }

                return new LanResponse
                {
                    serverId = ServerId,
                    uri = uri,
                    serverName = name,
                    currentPlayers = NetworkServer.connections.Count,
                    maxPlayers = maxPlayers,
                    mapName = map
                };
            }
            catch (System.NotImplementedException)
            {
                Debug.LogError($"[LanDiscovery] Transport {transport} has no ServerUri; LAN discovery disabled.");
                return default;
            }
        }

        // ------------------------------------------------------------------ //
        //  Client (browser) side
        // ------------------------------------------------------------------ //

        protected override void ProcessResponse(LanResponse response, IPEndPoint endpoint)
        {
            // Called on a worker thread — only touch thread-safe state.
            var server = new LanServer
            {
                serverId = response.serverId,
                uri = RewriteUriHost(response.uri, endpoint),
                serverName = response.serverName,
                currentPlayers = response.currentPlayers,
                maxPlayers = response.maxPlayers,
                mapName = response.mapName
            };

            lock (sync)
            {
                seen[server.serverId] = new SeenEntry { server = server, lastSeen = Time.time };
            }
        }

        static string RewriteUriHost(string uri, IPEndPoint endpoint)
        {
            try
            {
                // Clients must connect to the address the packet came from, not
                // whatever the host advertised (NIC/IP mismatch).
                UriBuilder builder = new UriBuilder(uri) { Host = endpoint.Address.ToString() };
                return builder.Uri.ToString();
            }
            catch
            {
                return uri;
            }
        }

        /// <summary>
        /// Snapshots fresh discoveries for the UI (main thread). Call from the
        /// browser refresh loop. Returns the core-facing view type.
        /// </summary>
        public List<LanServerInfo> Snapshot()
        {
            var list = new List<LanServerInfo>();
            lock (sync)
            {
                float now = Time.time;
                var stale = new List<long>();
                foreach (var kv in seen)
                {
                    if (now - kv.Value.lastSeen > maxStaleSeconds) { stale.Add(kv.Key); continue; }
                    LanServer s = kv.Value.server;
                    list.Add(new LanServerInfo
                    {
                        serverName = s.serverName,
                        currentPlayers = s.currentPlayers,
                        maxPlayers = s.maxPlayers,
                        mapName = s.mapName,
                        uri = s.uri,
                        host = TryGetHost(s.uri)
                    });
                }
                foreach (long id in stale) seen.Remove(id);
            }
            list.Sort((a, b) => string.Compare(a.serverName, b.serverName, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        static string TryGetHost(string uri)
        {
            try
            {
                if (string.IsNullOrEmpty(uri)) return "";
                string host = new Uri(uri).Host;
                return string.IsNullOrEmpty(host) ? "" : host;
            }
            catch { return ""; }
        }

        public void Clear()
        {
            lock (sync) seen.Clear();
        }
    }
}