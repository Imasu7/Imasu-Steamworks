using Mirror;
using System;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// NetworkManager for a listen-server session.
    /// The Steam lobby managers drive this: create lobby -> StartHost,
    /// join lobby -> StartClient, host presses start -> StartMatch.
    ///
    /// Scene flow (production layout):
    ///   offlineScene  = MainMenu     (not connected)
    ///   onlineScene   = WaitingRoom  (connected, waiting for the host to start)
    ///   StartMatch()  -> ServerChangeScene(matchScene)  (the actual game)
    /// Mirror loads onlineScene automatically on connect; StartMatch is a second,
    /// explicit scene change so players stay in the waiting room until then.
    /// </summary>
    public class ImasuNetworkManager : NetworkManager
    {
        /// <summary>A playable map: UI display name + scene name (must be in Build Settings).</summary>
        [Serializable]
        public class MapEntry
        {
            [Tooltip("Name shown in the map dropdown.")]
            public string displayName;
            [Tooltip("Scene name loaded by StartMatch.")]
            public string sceneName;
        }

        /// <summary>A gamemode: display name + its own map list (see ImasuSteamworksConfig).</summary>
        [Serializable]
        public class GameModeEntry
        {
            [Tooltip("Name shown in the gamemode dropdown.")]
            public string displayName;
            [Tooltip("Maps available for this gamemode.")]
            public MapEntry[] maps = Array.Empty<MapEntry>();
        }

        [Header("Session")]
        [Tooltip("Default match scene used when no override or lobby choice exists.")]
        public string matchScene = "Game";

        [Tooltip("Legacy map catalog (single 'Default' mode). Empty = fall back to matchScene only.")]
        public MapEntry[] maps = Array.Empty<MapEntry>();

        [Tooltip("Gamemode catalog pushed from ImasuSteamworksConfig. Each mode carries its own map list.")]
        public GameModeEntry[] gameModes = Array.Empty<GameModeEntry>();

        /// <summary>Host-only: scene for the next StartMatch when set (e.g., end-of-match rotation). Consumed by StartMatch.</summary>
        string pendingMatchScene;

        [Header("Local Test")]
        [Tooltip("Address used by StartLocalHost / StartLocalClient (KCP localhost tests).")]
        public string localAddress = "localhost";

        /// <summary>Gamemode display names, or a single "Default" mode when none configured.</summary>
        public string[] ModeDisplayNames
        {
            get
            {
                if (gameModes != null && gameModes.Length > 0)
                {
                    string[] names = new string[gameModes.Length];
                    for (int i = 0; i < gameModes.Length; i++)
                        names[i] = string.IsNullOrEmpty(gameModes[i].displayName)
                            ? "Mode " + (i + 1)
                            : gameModes[i].displayName;
                    return names;
                }
                return new[] { "Default" };
            }
        }

        /// <summary>Display names for a gamemode's maps (from gameModes, else legacy maps, else matchScene).</summary>
        public string[] MapDisplayNamesForMode(int modeIndex)
        {
            MapEntry[] modeMaps = ModeMaps(modeIndex);
            if (modeMaps.Length == 0) return new[] { matchScene };
            string[] names = new string[modeMaps.Length];
            for (int i = 0; i < modeMaps.Length; i++)
                names[i] = string.IsNullOrEmpty(modeMaps[i].displayName) ? modeMaps[i].sceneName : modeMaps[i].displayName;
            return names;
        }

        /// <summary>Resolves a gamemode+map index to a scene name, with fallbacks.</summary>
        public string MapSceneAt(int modeIndex, int mapIndex)
        {
            MapEntry[] modeMaps = ModeMaps(modeIndex);
            if (modeMaps.Length == 0) return matchScene;
            mapIndex = Mathf.Clamp(mapIndex, 0, modeMaps.Length - 1);
            return string.IsNullOrEmpty(modeMaps[mapIndex].sceneName) ? matchScene : modeMaps[mapIndex].sceneName;
        }

        /// <summary>Display names for the setup-screen dropdown (single-index legacy path = mode 0).</summary>
        public string[] MapDisplayNames => MapDisplayNamesForMode(0);

        /// <summary>Resolves a dropdown index to a scene name via maps[] (single-index legacy path = mode 0).</summary>
        public string MapSceneAtIndex(int index) => MapSceneAt(0, index);

        /// <summary>The map list backing a gamemode (gameModes first, then legacy maps).</summary>
        MapEntry[] ModeMaps(int modeIndex)
        {
            if (gameModes != null && gameModes.Length > 0)
            {
                int i = Mathf.Clamp(modeIndex, 0, gameModes.Length - 1);
                return gameModes[i].maps ?? Array.Empty<MapEntry>();
            }
            return maps ?? Array.Empty<MapEntry>();
        }

        /// <summary>Reverse lookup: friendly map name for a scene name (server browser rows).</summary>
        public string MapDisplayNameFor(string sceneName)
        {
            // Search gamemodes first, then legacy maps.
            if (gameModes != null)
                foreach (GameModeEntry mode in gameModes)
                    if (mode.maps != null)
                        foreach (MapEntry m in mode.maps)
                            if (!string.IsNullOrEmpty(m.sceneName) && m.sceneName == sceneName)
                                return string.IsNullOrEmpty(m.displayName) ? sceneName : m.displayName;
            if (maps != null)
                foreach (MapEntry m in maps)
                    if (!string.IsNullOrEmpty(m.sceneName) && m.sceneName == sceneName)
                        return string.IsNullOrEmpty(m.displayName) ? sceneName : m.displayName;
            return string.IsNullOrEmpty(sceneName) ? "?" : sceneName;
        }

        /// <summary>Host: queue an explicit scene for the next StartMatch (map rotation hook).</summary>
        public void SetNextMatchScene(string sceneName)
        {
            pendingMatchScene = sceneName;
        }

        /// <summary>
        /// Starts a local host (server + local client) for editor / LAN tests.
        /// Forces the KCP transport so no Steam session is required.
        /// </summary>
        public void StartLocalHost()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] Already active, host not started.");
                return;
            }

            UseLocalTransport();
            networkAddress = localAddress;
            StartHost();
            // Advertise over UDP so other machines can find this game in the
            // browser's LAN section (no Steam needed) — only when the LAN addon
            // is installed (ILanDiscovery present).
            LanDiscoveryQuery.Find()?.AdvertiseServer();
            Debug.Log($"[ImasuNetworkManager] Started local host at '{networkAddress}'.");
        }

        /// <summary>
        /// Starts a local client connecting to the configured localAddress.
        /// Forces the KCP transport so no Steam session is required.
        /// </summary>
        public void StartLocalClient()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] Already active, client not started.");
                return;
            }

            UseLocalTransport();
            networkAddress = localAddress;
            StartClient();
            Debug.Log($"[ImasuNetworkManager] Started local client connecting to '{networkAddress}'.");
        }

        /// <summary>
        /// Joins a LAN server at an arbitrary host/IP over KCP (no Steam).
        /// Used by the join screen's address path and the LAN browser rows.
        /// </summary>
        public void StartLanClient(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] Already active, client not started.");
                return;
            }

            UseLocalTransport();
            networkAddress = address.Trim();
            StartClient();
            Debug.Log($"[ImasuNetworkManager] Started LAN client connecting to '{networkAddress}'.");
        }

        /// <summary>
        /// Picks the KCP transport on this object for local tests. The object also
        /// carries the FizzyFacepunch (Steam) transport, and Mirror auto-selects the
        /// first one it finds — so we must choose explicitly for local runs.
        /// </summary>
        void UseLocalTransport()
        {
            foreach (Transport t in GetComponents<Transport>())
            {
                if (t.GetType().Name.Contains("Kcp") || t.GetType().Namespace?.StartsWith("kcp") == true)
                {
                    transport = t;
                    Transport.active = t;
                    return;
                }
            }
            Debug.LogWarning("[ImasuNetworkManager] No KcpTransport found for local test.");
        }

        /// <summary>
        /// Dedicated (headless, server-only) start over KCP — no Steam session or UI
        /// required. Used by the com.imasu.steamworks.dedicated addon's DedicatedHost.
        /// Port 0 keeps whatever the KcpTransport is configured with.
        /// </summary>
        public void StartDedicatedServer(int port = 0)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] Already active, dedicated server not started.");
                return;
            }
            UseLocalTransport();
            if (port > 0)
            {
                foreach (Transport t in GetComponents<Transport>())
                    if (t is kcp2k.KcpTransport kcp)
                        kcp.Port = (ushort)port;
            }
            StartServer();
            Debug.Log($"[ImasuNetworkManager] Dedicated server started (KCP, port {(port > 0 ? port : "config")}).");
        }

        public void StopLocalNetwork()
        {
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            else Debug.Log("[ImasuNetworkManager] No local session was active.");
        }

        /// <summary>
        /// Host only: changes everyone to the match scene. Mirror re-spawns a
        /// player prefab for every ready connection when the new scene loads.
        /// Priority: SetNextMatchScene override > matchScene field.
        /// Respects the config's minPlayers (1 = host can start solo).
        /// </summary>
        public bool StartMatch()
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] StartMatch requires an active server.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(pendingMatchScene))
            {
                matchScene = pendingMatchScene;
                pendingMatchScene = null;
                Debug.Log($"[ImasuNetworkManager] Using queued match scene '{matchScene}'.");
            }

            if (string.IsNullOrWhiteSpace(matchScene))
            {
                Debug.LogWarning("[ImasuNetworkManager] StartMatch needs matchScene set.");
                return false;
            }

            int min = ImasuSteamworksMaster.Config.EffectiveMinPlayers();
            int connected = 0;
            foreach (var c in NetworkServer.connections.Values)
                if (c.identity != null) connected++;
            if (connected < min)
            {
                Debug.LogWarning($"[ImasuNetworkManager] Need {min} player(s), only {connected} connected.");
                return false;
            }

            ServerChangeScene(matchScene);
            Debug.Log($"[ImasuNetworkManager] Server changing scene to '{matchScene}'.");
            return true;
        }

        /// <summary>
        /// Host only: switch everyone to a different map scene mid-session
        /// (in-match map rotation / change-map). Preserves the existing
        /// connections; Mirror re-spawns players for the new scene.
        /// </summary>
        public void ServerChangeMap(string sceneName)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[ImasuNetworkManager] ServerChangeMap requires an active server.");
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[ImasuNetworkManager] ServerChangeMap needs a scene name.");
                return;
            }

            matchScene = sceneName;
            pendingMatchScene = null;
            ServerChangeScene(sceneName);
            Debug.Log($"[ImasuNetworkManager] Server changed map to '{sceneName}'.");
        }

        void Start()
        {
            // Scoreboard is a passive singleton (hidden until Tab is held);
            // make sure it exists once any session object is present.
            ScoreboardUI.Ensure();
            // LAN discovery auto-initializes from the addon when installed.
        }

        /// <summary>Returns the online (waiting room) scene name, if configured.</summary>
        public string WaitingRoomScene => onlineScene;

        /// <summary>
        /// Spawns the player prefab at a team-filtered, anti-clump position from
        /// the scene's SpawnManager (SpawnPoint/SpawnArea). Falls back to Mirror's
        /// default (GetStartPosition / transform zero) when no spawns are placed.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // teamId 0 = any team (default single-team setup); team assignment can
            // be wired to TeamConfig per gamemode later.
            if (SpawnManager.TryGetSpawn(0, out Vector3 pos, out Quaternion rot))
            {
                GameObject player = Instantiate(playerPrefab, pos, rot);
                player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
                NetworkServer.AddPlayerForConnection(conn, player);
                return;
            }
            base.OnServerAddPlayer(conn);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            Debug.Log($"[ImasuNetworkManager] Server scene changed to '{sceneName}'.");
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            Debug.Log($"[ImasuNetworkManager] Client scene changed.");
        }

        /// <summary>Client connected: let the lobby manager clear its reconnect state.</summary>
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            if (SteamLobbyManager.instance != null) SteamLobbyManager.instance.OnMirrorConnected();
        }

        /// <summary>Client disconnected (host left / migrated): the lobby manager
        /// decides whether to become the new host or reconnect to the new owner.</summary>
        public override void OnClientDisconnect()
        {
            if (SteamLobbyManager.instance != null) SteamLobbyManager.instance.OnMirrorDisconnected();
            base.OnClientDisconnect();
        }
    }
}