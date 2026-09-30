using Mirror;
using Steamworks;
using global::Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Steam lobby session driver. Wires Steam lobbies to the Mirror session:
    ///   Create lobby  -> StartHost (listen server, Steam Relay via FizzyFacepunch)
    ///   Join lobby    -> StartClient using the lobby owner's Steam ID as the address
    /// Tracks current members so the waiting room (Slice 5) can render them.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager instance { get; private set; }

        [Header("Refs")]
        [Tooltip("NetworkManager used for StartHost / StartClient / StartMatch.")]
        public ImasuNetworkManager networkManager;

        [Header("Events")]
        public UnityEvent onLobbyCreated;
        public UnityEvent onLobbyJoined;
        public UnityEvent onLobbyLeft;
        public UnityEvent<int> onLobbyMemberCountChanged;

        /// <summary>Fired with a human-readable reason when a join attempt (by code or ID) fails.</summary>
        public UnityEvent<string> onJoinFailed;

        [Tooltip("Fired for each public lobby found by RequestLobbyList().")]
        public System.Action<Lobby> onLobbyListReceived;

        [Header("Session State (read only)")]
        public Lobby currentLobby;
        public bool inLobby;

        /// <summary>Host-set match settings (name/maxPlayers/scene/voiceRange), read from lobby data.</summary>
        public LobbySettings settings;

        public Dictionary<SteamId, string> members = new Dictionary<SteamId, string>();

        // Host migration state.
        bool becomingHost;
        int reconnectAttempts;
        const int MaxReconnectAttempts = 3;

        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (networkManager == null)
                networkManager = FindFirstObjectByType<ImasuNetworkManager>();
        }

        void OnEnable()
        {
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        void OnDisable()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected -= OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
        }

        public bool steamReady => SteamClient.IsValid;

        // ------------------------------------------------------------------ //
        //  Create / Join / Leave
        // ------------------------------------------------------------------ //

        /// <summary>Creates a public, joinable Steam lobby, then StartHosts Mirror.</summary>
        public void CreateLobby(LobbySettings setup)
        {
            if (!steamReady)
            {
                Debug.LogError("[SteamLobbyManager] Steam not initialized. Cannot create lobby.");
                return;
            }

            if (inLobby)
            {
                Debug.LogWarning("[SteamLobbyManager] Already in a lobby. Leave first.");
                return;
            }

            CreateLobbyAsync(setup);
        }

        /// <summary>Back-compat convenience: create a lobby with just a name/max players.</summary>
        public void CreateLobby(int maxPlayers = 8, string lobbyName = "Imasu Game")
        {
            LobbySettings s = LobbySettings.Defaults();
            s.maxPlayers = maxPlayers;
            s.lobbyName = lobbyName;
            CreateLobby(s);
        }

        async void CreateLobbyAsync(LobbySettings setup)
        {
            Lobby? created = await SteamMatchmaking.CreateLobbyAsync(setup.maxPlayers);
            if (!created.HasValue)
            {
                Debug.LogError("[SteamLobbyManager] Lobby creation returned no lobby.");
                return;
            }

            currentLobby = created.Value;
            ApplyLobbyVisibility(currentLobby, setup.security);
            currentLobby.SetJoinable(true);

            // Persist host settings as lobby data so joiners can read them.
            settings = setup;
            if (string.IsNullOrEmpty(settings.code)) settings.code = LobbyCode.Generate();
            currentLobby.SetData(LobbySettings.DataName, setup.lobbyName);
            currentLobby.SetData(LobbySettings.DataMaxPlayers, setup.maxPlayers.ToString());
            currentLobby.SetData(LobbySettings.DataScene, setup.sceneName);
            currentLobby.SetData(LobbySettings.DataGameMode, setup.gamemode ?? "");
            currentLobby.SetData(LobbySettings.DataVoiceRange, setup.voiceRange.ToString("0.##"));
            currentLobby.SetData(LobbySettings.DataPublic, setup.isPublic ? "1" : "0");
            currentLobby.SetData(LobbySettings.DataSecurity, ((int)setup.security).ToString());
            currentLobby.SetData(LobbySettings.DataCode, settings.code);
            currentLobby.SetData(LobbySettings.DataMarker, LobbySettings.MarkerValue);

            inLobby = true;
            Debug.Log($"[SteamLobbyManager] Lobby created: {currentLobby.Id} code {settings.code} ('{setup.lobbyName}', max {setup.maxPlayers}). Starting host.");

            StartMirrorHost();
        }

        /// <summary>Joins a lobby by its Steam lobby ID, then StartClients toward the owner.</summary>
        public async void JoinLobby(SteamId lobbyId)
        {
            if (!steamReady)
            {
                Debug.LogError("[SteamLobbyManager] Steam not initialized. Cannot join lobby.");
                onJoinFailed?.Invoke("Steam is not initialized.");
                return;
            }

            if (inLobby)
            {
                Debug.LogWarning("[SteamLobbyManager] Already in a lobby. Leave first.");
                onJoinFailed?.Invoke("Already in a lobby.");
                return;
            }

            Lobby? joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            if (!joined.HasValue)
            {
                Debug.LogError($"[SteamLobbyManager] Failed to join lobby {lobbyId}.");
                onJoinFailed?.Invoke("Could not join that lobby.");
                return;
            }

            // OnLobbyEntered handles currentLobby/inLobby/members + StartClient.
            // Here we just make sure state is consistent even if that event already fired.
            currentLobby = joined.Value;
            inLobby = true;
            RefreshMembers();
        }

        /// <summary>
        /// Joins via short shareable code ("HG6FY14"). Searches public lobbies for
        /// a matching <see cref="LobbySettings.DataCode"/> entry. Input longer than
        /// a code that parses as a number is treated as a raw Steam lobby ID
        /// (back-compat with the old 19-digit codes).
        /// </summary>
        public async void JoinByCode(string rawInput)
        {
            string input = LobbyCode.Normalize(rawInput);
            if (string.IsNullOrEmpty(input))
            {
                onJoinFailed?.Invoke("Enter a lobby code first.");
                return;
            }

            if (!steamReady)
            {
                Debug.LogError("[SteamLobbyManager] Steam not initialized. Cannot join by code.");
                onJoinFailed?.Invoke("Steam is not initialized.");
                return;
            }

            if (inLobby)
            {
                Debug.LogWarning("[SteamLobbyManager] Already in a lobby. Leave first.");
                onJoinFailed?.Invoke("Already in a lobby.");
                return;
            }

            if (LobbyCode.LooksLikeRawLobbyId(input))
            {
                JoinLobby((SteamId)ulong.Parse(input));
                return;
            }

            try
            {
                Lobby[] found = await SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithKeyValue(LobbySettings.DataCode, input)
                    .WithMaxResults(1)
                    .RequestAsync();

                if (found == null || found.Length == 0)
                {
                    onJoinFailed?.Invoke($"No lobby found for code '{input}'.");
                    return;
                }

                JoinLobby(found[0].Id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamLobbyManager] Code search failed: {e.Message}");
                onJoinFailed?.Invoke("Lobby search failed.");
            }
        }

        /// <summary>Leaves the current lobby and stops the Mirror session. Also handles
        /// non-Steam (KCP) sessions where there is no lobby — it just tears the session down.
        ///
        /// When a HOST leaves a lobby with other members, ownership is transferred to the
        /// next member first — Steam destroys a lobby whose owner leaves without handing
        /// off — and the remaining players migrate (see <see cref="OnClientDisconnect"/>).</summary>
        public async void LeaveLobby()
        {
            if (NetworkServer.active && inLobby && members.Count > 1)
            {
                Friend? next = PickNextHost();
                if (next.HasValue)
                {
                    try
                    {
                        currentLobby.Owner = next.Value;
                        Debug.Log($"[SteamLobbyManager] Handing host to {next.Value.Name}; leaving after the transfer lands.");
                        await Task.Delay(1200);
                        if (!inLobby) return; // user already left meanwhile
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[SteamLobbyManager] Ownership handoff failed ({e.Message}); leaving anyway.");
                    }
                }
            }
            DoLeave();
        }

        void DoLeave()
        {
            bool hadLobby = inLobby;
            if (hadLobby)
            {
                currentLobby.Leave();
                inLobby = false;
                members.Clear();
                onLobbyLeft.Invoke();
            }

            bool hadSession = NetworkServer.active || NetworkClient.active;
            if (NetworkServer.active && NetworkClient.active) networkManager?.StopHost();
            else if (NetworkClient.active) networkManager?.StopClient();
            else if (NetworkServer.active) networkManager?.StopServer();

            if (hadLobby)
                Debug.Log("[SteamLobbyManager] Left lobby.");
            else if (hadSession)
                Debug.Log("[SteamLobbyManager] Left local (non-Steam) session.");
            else
                Debug.Log("[SteamLobbyManager] LeaveLobby: no session was active.");
        }

        /// <summary>Oldest non-self member, i.e. the natural next host.</summary>
        Friend? PickNextHost()
        {
            foreach (KeyValuePair<SteamId, string> kv in members)
                if (kv.Key != SteamClient.SteamId)
                    return new Friend(kv.Key);
            return null;
        }

        // ------------------------------------------------------------------ //
        //  Match start
        // ------------------------------------------------------------------ //

        /// <summary>Host only: load the match scene for everyone. Mirror spawns players.
        /// Enforces the gamemode's effective minPlayers (1 = host can start solo).</summary>
        public bool StartMatch()
        {
            if (!inLobby || !NetworkServer.active)
            {
                Debug.LogWarning("[SteamLobbyManager] StartMatch needs an active host in a lobby.");
                return false;
            }

            int modeIndex = settings.modeIndex >= 0
                ? settings.modeIndex
                : ImasuSteamworksMaster.Config.ModeIndexByName(settings.gamemode);
            int min = ImasuSteamworksMaster.Config.EffectiveMinPlayers(modeIndex);
            if (members.Count < min)
            {
                Debug.LogWarning($"[SteamLobbyManager] Need {min} player(s) in the lobby, only {members.Count}.");
                return false;
            }

            // Apply the host's chosen scene to the manager, then change scene.
            if (!string.IsNullOrEmpty(settings.sceneName))
                networkManager.matchScene = settings.sceneName;

            return networkManager.StartMatch();
        }

        /// <summary>
        /// Host: queue a specific scene for the next StartMatch without rewriting
        /// the lobby's stored map choice — hook for future end-of-match rotation.
        /// </summary>
        public void SetNextMap(string sceneName) => networkManager?.SetNextMatchScene(sceneName);

        /// <summary>
        /// Host: switch everyone to a new map scene mid-session AND update the
        /// lobby's advertised scene so the server browser shows the new map.
        /// </summary>
        public void ServerChangeMap(string sceneName, string modeName = null)
        {
            if (!inLobby || !NetworkServer.active)
            {
                Debug.LogWarning("[SteamLobbyManager] ServerChangeMap needs an active host in a lobby.");
                return;
            }

            settings.sceneName = sceneName;
            if (modeName != null) settings.gamemode = modeName;
            currentLobby.SetData(LobbySettings.DataScene, sceneName);
            currentLobby.SetData(LobbySettings.DataGameMode, settings.gamemode ?? "");
            networkManager?.ServerChangeMap(sceneName);
        }

        // ------------------------------------------------------------------ //
        //  Steam event handlers
        // ------------------------------------------------------------------ //

        void StartMirrorHost()
        {
            if (networkManager == null)
            {
                Debug.LogError("[SteamLobbyManager] No ImasuNetworkManager to host with.");
                return;
            }

            networkManager.StartHost();
            onLobbyCreated.Invoke();
        }

        void OnLobbyCreated(Result result, Lobby lobby)
        {
            if (result != Result.OK)
                Debug.LogWarning($"[SteamLobbyManager] Lobby created with result: {result}");
        }

        void OnLobbyEntered(Lobby lobby)
        {
            currentLobby = lobby;
            inLobby = true;
            settings = ReadSettingsFromLobby(lobby);
            reconnectAttempts = 0;
            RefreshMembers();
            onLobbyJoined.Invoke();
            Debug.Log($"[SteamLobbyManager] Entered lobby {lobby.Id}. Owner: {lobby.Owner.Id}");

            // Host already runs a server; only clients connect to the owner.
            if (NetworkServer.active) return;

            if (networkManager == null)
            {
                Debug.LogError("[SteamLobbyManager] No ImasuNetworkManager to connect with.");
                return;
            }

            networkManager.networkAddress = lobby.Owner.Id.ToString();
            networkManager.StartClient();
            Debug.Log("[SteamLobbyManager] Mirror client started.");
        }

        /// <summary>
        /// Asks Steam for public lobbies matching our app id. Results are delivered
        /// one-by-one via <see cref="onLobbyListReceived"/>.
        /// </summary>
        public void RequestLobbyList()
        {
            if (!steamReady) return;

            SteamMatchmaking.LobbyList
                .WithSlotsAvailable(1)
                .WithMaxResults(12)
                .RequestAsync()
                .ContinueWith(t =>
                {
                    if (t.Result == null) return;
                    foreach (Lobby lobby in t.Result)
                        onLobbyListReceived?.Invoke(lobby);
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        LobbySettings ReadSettingsFromLobby(Lobby lobby)
        {
            LobbySettings s = LobbySettings.Defaults();
            s.lobbyName = lobby.GetData(LobbySettings.DataName);
            if (string.IsNullOrEmpty(s.lobbyName)) s.lobbyName = "Imasu Game";
            if (int.TryParse(lobby.GetData(LobbySettings.DataMaxPlayers), out int mp)) s.maxPlayers = mp;
            s.sceneName = lobby.GetData(LobbySettings.DataScene);
            s.gamemode = lobby.GetData(LobbySettings.DataGameMode);
            if (float.TryParse(lobby.GetData(LobbySettings.DataVoiceRange), out float vr)) s.voiceRange = vr;
            s.code = lobby.GetData(LobbySettings.DataCode);

            if (Enum.TryParse(lobby.GetData(LobbySettings.DataSecurity), out LobbySecurity sec))
            {
                s.security = sec;
                s.isPublic = s.security == LobbySecurity.Open;
            }
            else
            {
                // Legacy lobbies created before the security key existed.
                s.isPublic = lobby.GetData(LobbySettings.DataPublic) == "1";
                s.security = s.isPublic ? LobbySecurity.Open : LobbySecurity.FriendsOnly;
            }
            return s;
        }

        /// <summary>Maps our security choice onto Steam lobby visibility.</summary>
        static void ApplyLobbyVisibility(Lobby lobby, LobbySecurity security)
        {
            switch (security)
            {
                case LobbySecurity.InviteOnly: lobby.SetPrivate(); break;
                case LobbySecurity.FriendsOnly: lobby.SetFriendsOnly(); break;
                default: lobby.SetPublic(); break;
            }
        }

        void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            RefreshMembers();
            Debug.Log($"[SteamLobbyManager] {friend.Name} joined the lobby.");
        }

        void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            RefreshMembers();
            Debug.Log($"[SteamLobbyManager] {friend.Name} left the lobby.");
        }

        void OnGameLobbyJoinRequested(Lobby lobby, SteamId ownerId)
        {
            Debug.Log($"[SteamLobbyManager] Join request for lobby {lobby.Id}.");
            JoinLobby(lobby.Id);
        }

        // ------------------------------------------------------------------ //
        //  Host migration
        // ------------------------------------------------------------------ //

        void Update()
        {
            // Safety net: this Facepunch build has no OnLobbyOwnerChanged event, so
            // ownership is only visible via Lobby.Owner. If we own the lobby while
            // connected to nothing, the old host is gone — take over. (The normal
            // path is OnClientDisconnect; this catches a dropped event.)
            if (!inLobby || NetworkServer.active || NetworkClient.active) return;
            if (Time.time < nextOwnerPoll) return;
            nextOwnerPoll = Time.time + 0.5f;
            if (currentLobby.Owner.Id == SteamClient.SteamId)
                BecomeHost();
        }

        float nextOwnerPoll = float.MinValue;

        /// <summary>Promote the local client to host after a lobby ownership handoff.
        /// Tears down the old client connection, then StartHosts on our Steam ID —
        /// remaining clients reconnect to us via <see cref="currentLobby.Owner"/>.</summary>
        public bool BecomeHost()
        {
            if (!inLobby) return false;
            if (networkManager == null)
            {
                Debug.LogError("[SteamLobbyManager] No ImasuNetworkManager to host with.");
                return false;
            }

            settings = ReadSettingsFromLobby(currentLobby);

            if (NetworkServer.active)
            {
                Debug.Log("[SteamLobbyManager] Already hosting — ignoring ownership handoff.");
                return true;
            }

            if (NetworkClient.active)
            {
                becomingHost = true;
                try { networkManager.StopClient(); }
                finally { becomingHost = false; }
            }

            networkManager.networkAddress = currentLobby.Owner.Id.ToString();
            networkManager.StartHost();
            onLobbyCreated.Invoke();
            Debug.Log("[SteamLobbyManager] Became the new host after migration.");
            return true;
        }

        /// <summary>Called from ImasuNetworkManager.OnClientConnect (client side).</summary>
        public void OnMirrorConnected()
        {
            reconnectAttempts = 0;
        }

        /// <summary>Called from ImasuNetworkManager.OnClientDisconnect (client side).
        /// If the host left, either take over (when Steam gave us ownership) or
        /// reconnect to the current lobby owner.</summary>
        public void OnMirrorDisconnected()
        {
            if (!inLobby) return;
            if (becomingHost) return;

            if (currentLobby.Owner.Id == SteamClient.SteamId)
            {
                BecomeHost();
                return;
            }

            SteamId owner = currentLobby.Owner.Id;
            if (owner == 0 || owner == SteamClient.SteamId) return;

            if (reconnectAttempts >= MaxReconnectAttempts)
            {
                Debug.LogWarning("[SteamLobbyManager] Reconnect failed repeatedly — leaving the lobby.");
                onJoinFailed?.Invoke("Lost the host and could not reconnect.");
                DoLeave();
                return;
            }

            reconnectAttempts++;
            networkManager.networkAddress = owner.ToString();
            networkManager.StartClient();
            Debug.Log($"[SteamLobbyManager] Host disconnected — reconnecting to new host {owner} (attempt {reconnectAttempts}).");
        }

        void RefreshMembers()
        {
            members.Clear();
            foreach (Friend member in currentLobby.Members)
                members[member.Id] = member.Name;
            onLobbyMemberCountChanged.Invoke(members.Count);
        }
    }
}