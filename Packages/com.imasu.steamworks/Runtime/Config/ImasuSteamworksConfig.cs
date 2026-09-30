using System;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Single source of truth for every tunable in the Imasu Steamworks stack.
    /// Edit it here (or via Imasu → Steamworks Control Center) — never hunt
    /// through six scripts. The <see cref="ImasuSteamworksMaster"/> on the
    /// ImasuSessionManager object pushes these values into the live components
    /// (SteamManager, FizzyFacepunch, KcpTransport, ImasuNetworkManager,
    /// VoicePlayer, lobby defaults) at runtime.
    ///
    /// Voice semantics:
    ///   useDistanceAttenuation = OFF (Fixed) — everyone is heard at full volume
    ///       regardless of distance. Players only tune the global VOIP volume.
    ///   useDistanceAttenuation = ON  (Range) — logarithmic rolloff between
    ///       voipMinDistance and voipMaxDistance; maxDistance also becomes the
    ///       lobby's advertised voice range.
    ///
    /// Maps & modes (Control Center 2.0):
    ///   gameModes[] is the modern catalog: each mode carries its own map list,
    ///   shown as two dropdowns on Host Setup (mode first, then its maps), and
    ///   an optional sparse <see cref="GameModeOverrides"/> set (toggle + value
    ///   per field, falling back to the global when unticked). Each map can also
    ///   override a couple of map-scoped values (player cap). Effective values
    ///   resolve: map override → gamemode override → global.
    ///   The legacy maps[] array still works and is treated as a single implicit
    ///   "Default" mode when gameModes is empty.
    /// </summary>
    [CreateAssetMenu(fileName = "ImasuSteamworksConfig", menuName = "Imasu/Steamworks Config")]
    public class ImasuSteamworksConfig : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Addon
        // ------------------------------------------------------------------ //

        [Header("Addon")]
        [Tooltip("Master kill-switch for the whole addon. OFF = the addon is fully suppressed (no hard errors, no Steam init, no session UI blocking) and the game runs as single-player. Flip it from the Control Center or this asset — one click applies everywhere.")]
        public bool addonEnabled = true;

        // ------------------------------------------------------------------ //
        //  Scenes & Modes
        // ------------------------------------------------------------------ //

        [Header("Scenes & Modes")]
        [Tooltip("Main menu scene (Mirror offlineScene). Loaded at boot and when the session stops.")]
        public string mainMenuScene = "MainMenu";

        [Tooltip("Lobby / waiting-room scene (Mirror onlineScene). Set it equal to mainMenuScene to share ONE scene for both menu and lobby.")]
        public string lobbyScene = "WaitingRoom";

        [Tooltip("Gamemode catalog. Each mode lists its own maps and (optionally) sparse overrides. Empty = fall back to the legacy maps[] array as a single 'Default' mode.")]
        public GameModeEntry[] gameModes = Array.Empty<GameModeEntry>();

        [Serializable]
        public class GameModeEntry
        {
            [Tooltip("Name shown in the gamemode dropdown, e.g. 'Elimination' or 'Raid'.")]
            public string displayName = "Default";

            [Tooltip("Maps available for this gamemode. Leave empty to fall back to the legacy maps[]. Spawns are limited to the physical SpawnPoint/SpawnArea components in each map's scene.")]
            public MapEntry[] maps = Array.Empty<MapEntry>();

            [Tooltip("Optional per-gamemode overrides. Unticked fields inherit the global value (see 'Overrides' foldout in the Control Center).")]
            public GameModeOverrides overrides = new GameModeOverrides();
        }

        /// <summary>Curated sparse overrides a gamemode can set. Each field only
        /// takes effect when its matching override_ bool is ticked; otherwise the
        /// global config value is used. Steam identity / scenes / kcpPort are
        /// intentionally NOT overridable here.</summary>
        [Serializable]
        public class GameModeOverrides
        {
            [Tooltip("Override maxPlayers for this gamemode.")]
            public bool override_maxPlayers;
            public int maxPlayers = 8;

            [Tooltip("Override minPlayers for this gamemode.")]
            public bool override_minPlayers;
            public int minPlayers = 1;

            [Tooltip("Override the lobby/server name prefix for this gamemode.")]
            public bool override_lobbyName;
            public string lobbyName = "";

            [Tooltip("Override default lobby security for this gamemode.")]
            public bool override_security;
            public LobbySecurity security = LobbySecurity.Open;

            [Tooltip("Override voipEnabled for this gamemode.")]
            public bool override_voipEnabled;
            public bool voipEnabled = true;

            [Tooltip("Override voice Fixed vs Range for this gamemode.")]
            public bool override_useDistanceAttenuation;
            public bool useDistanceAttenuation = false;

            [Tooltip("Override voipMinDistance for this gamemode.")]
            public bool override_voipMinDistance;
            public float voipMinDistance = 1f;

            [Tooltip("Override voipMaxDistance for this gamemode.")]
            public bool override_voipMaxDistance;
            public float voipMaxDistance = 30f;

            [Tooltip("Override default per-user VOIP volume for this gamemode.")]
            public bool override_voipUserVolumeDefault;
            public float voipUserVolumeDefault = 1f;

            [Tooltip("Override mover walk speed for this gamemode.")]
            public bool override_walkSpeed;
            public float walkSpeed = 5f;

            [Tooltip("Override mover sprint multiplier for this gamemode.")]
            public bool override_sprintMultiplier;
            public float sprintMultiplier = 1.6f;

            [Tooltip("Override mover jump speed for this gamemode.")]
            public bool override_jumpSpeed;
            public float jumpSpeed = 7f;

            [Tooltip("Override mover gravity for this gamemode.")]
            public bool override_gravity;
            public float gravity = 20f;

            [Tooltip("Override mover mouse sensitivity for this gamemode.")]
            public bool override_mouseSensitivity;
            public float mouseSensitivity = 2.5f;

            [Tooltip("Override P2P timeout for this gamemode.")]
            public bool override_p2pTimeoutSeconds;
            public int p2pTimeoutSeconds = 25;

            [Tooltip("Override Steam relay fallback for this gamemode.")]
            public bool override_allowSteamRelay;
            public bool allowSteamRelay = true;

            [Tooltip("Override next-gen SteamSockets for this gamemode.")]
            public bool override_useNextGenSteamNetworking;
            public bool useNextGenSteamNetworking = true;
        }

        // ------------------------------------------------------------------ //
        //  Session
        // ------------------------------------------------------------------ //

        [Header("Session")]
        [Tooltip("Soft cap on players for a server. Also clamps NetworkManager.maxConnections. Gamemodes/maps can override this.")]
        [Range(2, 32)]
        public int maxPlayers = 8;

        [Tooltip("Minimum players required before the host can start the match. Set to 1 (default) so the host can start solo.")]
        [Range(1, 32)]
        public int minPlayers = 1;

        [Tooltip("Default lobby/server name shown in Game Setup.")]
        public string lobbyName = "Imasu Game";

        [Tooltip("Default join security for a hosted lobby.")]
        public LobbySecurity security = LobbySecurity.Open;

        [Tooltip("Lobby code length used when generating short join codes.")]
        [Range(4, 12)]
        public int lobbyCodeLength = 7;

        // ------------------------------------------------------------------ //
        //  Steam
        // ------------------------------------------------------------------ //

        [Header("Steam")]
        [Tooltip("Steamworks App ID. 480 = Spacewar (dev default). Set your real App ID before public builds. NOT overridable per-gamemode.")]
        public uint steamAppId = 480;

        [Tooltip("Require the Steam client to be running to initialize Steamworks.")]
        public bool requireSteam = true;

        // ------------------------------------------------------------------ //
        //  Voice
        // ------------------------------------------------------------------ //

        [Header("Voice (global defaults)")]
        [Tooltip("Master kill-switch for the whole voice system.")]
        public bool voipEnabled = true;

        [Tooltip("OFF = Fixed volume (no distance dropoff; everyone heard at full volume). ON = distance attenuation with min/max range.")]
        public bool useDistanceAttenuation = false;

        [Tooltip("Range mode: distance at which volume starts rolling off.")]
        [Range(0.1f, 500f)]
        public float voipMinDistance = 1f;

        [Tooltip("Range mode: distance at which a speaker is inaudible. Also advertised as the lobby's voice range.")]
        [Range(1f, 500f)]
        public float voipMaxDistance = 30f;

        [Tooltip("Default per-user VOIP volume multiplier (player-adjustable in Settings → Audio).")]
        [Range(0f, 2f)]
        public float voipUserVolumeDefault = 1f;

        // ------------------------------------------------------------------ //
        //  Debug
        // ------------------------------------------------------------------ //

        [Header("Debug")]
        [Tooltip("Allow hosting/joining non-Steam LAN (KCP) servers. When OFF, the Game Setup 'Use Steam networking' checkbox and the Join screen's LAN browser are hidden. Listed for debug purposes.")]
        public bool allowNonSteamServers = true;

        // ------------------------------------------------------------------ //
        //  Maps (legacy)
        // ------------------------------------------------------------------ //

        [Header("Maps (legacy — use gameModes above)")]
        [Tooltip("Legacy map catalog used when gameModes is empty. Prefer defining gamemodes above.")]
        public MapEntry[] maps = Array.Empty<MapEntry>();

        [Serializable]
        public class MapEntry
        {
            [Tooltip("Name shown in the map dropdown.")]
            public string displayName = "Courtyard";

            [Tooltip("Scene name loaded by StartMatch. The scene must contain an ImasuSessionManager (it won't be listed otherwise).")]
            public string sceneName = "Game";

            [Tooltip("Optional per-map player cap (e.g. a small map fits fewer players). 0 = inherit from gamemode/global.")]
            public bool override_maxPlayers;
            [Range(2, 32)]
            public int maxPlayers = 8;

            [Tooltip("Optional note shown to the host in the Control Center (e.g. 'small map — 6v6').")]
            public string note = "";
        }

        // ------------------------------------------------------------------ //
        //  Spawns & Teams
        // ------------------------------------------------------------------ //

        [Header("Spawns & Teams")]
        [Tooltip("Team + anti-clump config used by the scene's SpawnManager. Create via Assets > Create > Imasu > Team Config, or let the Control Center make one for you.")]
        public ImasuTeamConfig teamConfig;

        // ------------------------------------------------------------------ //
        //  Transport (dependencies)
        // ------------------------------------------------------------------ //

        [Header("Transport (Mirror / FizzyFacepunch / KCP)")]
        [Tooltip("Connection timeout in seconds before a peer is dropped.")]
        [Range(5, 120)]
        public int p2pTimeoutSeconds = 25;

        [Tooltip("Allow P2P connections to fall back to Steam Relay when NAT blocks direct.")]
        public bool allowSteamRelay = true;

        [Tooltip("Use SteamSockets (next-gen networking) instead of the deprecated legacy API.")]
        public bool useNextGenSteamNetworking = true;

        [Tooltip("Local KCP listen port for non-Steam LAN sessions. NOT overridable per-gamemode.")]
        [Range(1024, 65535)]
        public int kcpPort = 7777;

        // ------------------------------------------------------------------ //
        //  First-person mover (player prefab)
        // ------------------------------------------------------------------ //

        [Header("Player Mover (SimpleFirstPersonController)")]
        [Range(0.1f, 30f)]
        public float walkSpeed = 5f;

        [Range(1f, 5f)]
        public float sprintMultiplier = 1.6f;

        [Range(0f, 30f)]
        public float jumpSpeed = 7f;

        [Range(0.1f, 30f)]
        public float gravity = 20f;

        [Range(0.1f, 10f)]
        public float mouseSensitivity = 2.5f;

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        public bool HasGameModes => gameModes != null && gameModes.Length > 0;

        /// <summary>Gamemode display names, or a single "Default" mode when only legacy maps exist.</summary>
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

        /// <summary>The gamemode at an index, or a synthetic Default when gameModes is empty.</summary>
        public GameModeEntry ModeAt(int modeIndex)
        {
            if (gameModes != null && gameModes.Length > 0)
                return gameModes[Mathf.Clamp(modeIndex, 0, gameModes.Length - 1)];
            return new GameModeEntry { displayName = "Default", maps = maps };
        }

        /// <summary>Maps for a gamemode index, falling back to the legacy maps[] when gameModes is empty.</summary>
        public MapEntry[] MapsForMode(int modeIndex)
        {
            GameModeEntry mode = ModeAt(modeIndex);
            return mode.maps ?? Array.Empty<MapEntry>();
        }

        /// <summary>Display names for a gamemode's maps (empty if none configured).</summary>
        public string[] MapDisplayNamesForMode(int modeIndex)
        {
            MapEntry[] modeMaps = MapsForMode(modeIndex);
            if (modeMaps.Length == 0) return Array.Empty<string>();
            string[] names = new string[modeMaps.Length];
            for (int i = 0; i < modeMaps.Length; i++)
                names[i] = string.IsNullOrEmpty(modeMaps[i].displayName) ? modeMaps[i].sceneName : modeMaps[i].displayName;
            return names;
        }

        /// <summary>Scene name for a gamemode+map index, or null if not resolvable.</summary>
        public string SceneFor(int modeIndex, int mapIndex)
        {
            MapEntry[] modeMaps = MapsForMode(modeIndex);
            if (modeMaps.Length == 0) return null;
            mapIndex = Mathf.Clamp(mapIndex, 0, modeMaps.Length - 1);
            return string.IsNullOrEmpty(modeMaps[mapIndex].sceneName) ? null : modeMaps[mapIndex].sceneName;
        }

        /// <summary>First configured map scene, or null. Used to seed matchScene.</summary>
        public string FirstMapScene()
        {
            MapEntry[] modeMaps = MapsForMode(0);
            if (modeMaps.Length > 0 && !string.IsNullOrEmpty(modeMaps[0].sceneName))
                return modeMaps[0].sceneName;
            return null;
        }

        /// <summary>Index of a gamemode by display name (case-insensitive), or -1.</summary>
        public int ModeIndexByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            if (gameModes != null)
                for (int i = 0; i < gameModes.Length; i++)
                    if (string.Equals(gameModes[i].displayName, name, StringComparison.OrdinalIgnoreCase))
                        return i;
            return -1;
        }

        // ------------------------------------------------------------------ //
        //  Effective values: map override → gamemode override → global
        // ------------------------------------------------------------------ //

        MapEntry MapAt(int modeIndex, int mapIndex)
        {
            MapEntry[] modeMaps = ModeAt(modeIndex).maps ?? Array.Empty<MapEntry>();
            if (modeMaps.Length == 0) return null;
            return modeMaps[Mathf.Clamp(mapIndex, 0, modeMaps.Length - 1)];
        }

        public int EffectiveMaxPlayers(int modeIndex = -1, int mapIndex = -1)
        {
            MapEntry map = mapIndex >= 0 ? MapAt(modeIndex, mapIndex) : null;
            if (map != null && map.override_maxPlayers && map.maxPlayers > 0) return map.maxPlayers;
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_maxPlayers && mode.overrides.maxPlayers > 0)
                return mode.overrides.maxPlayers;
            return maxPlayers;
        }

        public int EffectiveMinPlayers(int modeIndex = -1, int mapIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_minPlayers) return Mathf.Max(1, mode.overrides.minPlayers);
            return minPlayers;
        }

        public string EffectiveLobbyName(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_lobbyName && !string.IsNullOrEmpty(mode.overrides.lobbyName))
                return mode.overrides.lobbyName;
            return lobbyName;
        }

        public LobbySecurity EffectiveSecurity(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_security)
                return mode.overrides.security;
            return security;
        }

        public bool EffectiveVoipEnabled(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_voipEnabled) return mode.overrides.voipEnabled;
            return voipEnabled;
        }

        public bool EffectiveUseDistanceAttenuation(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_useDistanceAttenuation)
                return mode.overrides.useDistanceAttenuation;
            return useDistanceAttenuation;
        }

        public float EffectiveVoipMinDistance(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_voipMinDistance)
                return mode.overrides.voipMinDistance;
            return voipMinDistance;
        }

        public float EffectiveVoipMaxDistance(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_voipMaxDistance)
                return mode.overrides.voipMaxDistance;
            return voipMaxDistance;
        }

        public float EffectiveVoipUserVolumeDefault(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_voipUserVolumeDefault)
                return mode.overrides.voipUserVolumeDefault;
            return voipUserVolumeDefault;
        }

        public float EffectiveWalkSpeed(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_walkSpeed) return mode.overrides.walkSpeed;
            return walkSpeed;
        }

        public float EffectiveSprintMultiplier(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_sprintMultiplier) return mode.overrides.sprintMultiplier;
            return sprintMultiplier;
        }

        public float EffectiveJumpSpeed(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_jumpSpeed) return mode.overrides.jumpSpeed;
            return jumpSpeed;
        }

        public float EffectiveGravity(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_gravity) return mode.overrides.gravity;
            return gravity;
        }

        public float EffectiveMouseSensitivity(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_mouseSensitivity) return mode.overrides.mouseSensitivity;
            return mouseSensitivity;
        }

        public int EffectiveP2pTimeoutSeconds(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_p2pTimeoutSeconds)
                return Mathf.Max(1, mode.overrides.p2pTimeoutSeconds);
            return p2pTimeoutSeconds;
        }

        public bool EffectiveAllowSteamRelay(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_allowSteamRelay) return mode.overrides.allowSteamRelay;
            return allowSteamRelay;
        }

        public bool EffectiveUseNextGenSteamNetworking(int modeIndex = -1)
        {
            GameModeEntry mode = modeIndex >= 0 ? ModeAt(modeIndex) : null;
            if (mode != null && mode.overrides.override_useNextGenSteamNetworking)
                return mode.overrides.useNextGenSteamNetworking;
            return useNextGenSteamNetworking;
        }

        void OnValidate()
        {
            minPlayers = Mathf.Clamp(minPlayers, 1, maxPlayers);
            if (voipMaxDistance < voipMinDistance) voipMaxDistance = voipMinDistance;

            if (gameModes != null)
                foreach (GameModeEntry mode in gameModes)
                {
                    if (mode.overrides.override_minPlayers)
                        mode.overrides.minPlayers = Mathf.Clamp(mode.overrides.minPlayers, 1, EffectiveMaxPlayersOf(mode));
                    if (mode.overrides.override_voipMaxDistance && mode.overrides.voipMaxDistance < mode.overrides.voipMinDistance)
                        mode.overrides.voipMaxDistance = mode.overrides.voipMinDistance;
                }
        }

        int EffectiveMaxPlayersOf(GameModeEntry mode)
        {
            if (mode.overrides.override_maxPlayers && mode.overrides.maxPlayers > 0) return mode.overrides.maxPlayers;
            return maxPlayers;
        }

        public bool UseDistanceVoice => voipEnabled && useDistanceAttenuation;
    }
}