using Mirror;
using Steamworks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Session UI for the production flow (R1/R2). Builds its own uGUI at runtime.
    ///
    /// Screens:
    ///   Main Menu   (MainMenu scene)  - Create Game, Join Game, Local Host,
    ///                                   Local Client, Quit
    ///   Game Setup  (host, pre-lobby) - lobby name, max players, map, voice
    ///                                   range, public/friends-only, Back
    ///   Waiting Room (online scene)   - lobby code + copy, member list,
    ///                                   Start Match (host), Leave, Push-to-talk
    /// Which screen is shown is driven by session state, so the same component
    /// works in both the MainMenu scene and the WaitingRoom scene.
    /// </summary>
    public class ImasuSessionUI : MonoBehaviour
    {
        [Header("Refs (auto-found if empty)")]
        public SteamLobbyManager lobbyManager;
        public ImasuNetworkManager networkManager;
        public SteamManager steamManager;

        [Header("Options")]
        [Tooltip("Scene list offered in Game Setup's map dropdown.")]
        public string[] availableScenes = { "Game" };

        Canvas canvas;
        GameObject menuScreen;
        GameObject joinScreen;
        GameObject setupScreen;
        GameObject waitingScreen;

        // menu controls
        TextMeshProUGUI menuSteamLabel;
        TextMeshProUGUI menuStatusLabel;

        // setup controls
        TMP_InputField setupName;
        TMP_InputField setupMaxPlayers;
        SimpleDropdown setupMode;
        SimpleDropdown setupScene;
        TMP_InputField setupVoiceRange;
        SimpleDropdown setupSecurity;
        GameObject setupUseSteamCheck;
        TextMeshProUGUI addonBanner;

        // join screen controls
        TMP_InputField joinCodeInput;
        TextMeshProUGUI joinStatusLabel;
        GameObject codeView;
        GameObject browserView;
        TMP_InputField browserFilter;
        RectTransform browserContent;
        TextMeshProUGUI browserStatusLabel;
        bool browsing;

        // waiting room controls
        RectTransform memberList;
        TextMeshProUGUI waitingLobbyInfo;
        TextMeshProUGUI lobbyCodeField;
        GameObject startMatchButton;
        GameObject pushToTalkButton;
        GameObject inviteButton;
        TextMeshProUGUI waitingStatusLabel;
        TextMeshProUGUI voiceStatsLabel;
        float lastCodeCopyTime = float.MinValue;

        // invite controls
        GameObject inviteScreen;
        TMP_InputField inviteSearch;
        RectTransform inviteContent;
        TextMeshProUGUI inviteStatusLabel;
        readonly List<InviteRowEntry> inviteRows = new List<InviteRowEntry>();

        // chat controls
        RectTransform chatLog;
        ScrollRect chatScroll;
        TMP_InputField chatInput;
        GameObject chatSendButton;
        TextMeshProUGUI chatHintLabel;
        int lastChatCount = -1;

        /// <summary>Pooled invite-list row. Reused across filter refreshes so the
        /// AvatarLateAssign subscription survives and we stop destroy/recreate churn
        /// (friends lists are routinely 100+).</summary>
        class InviteRowEntry
        {
            public GameObject go;
            public Image rowImg;
            public RawImage avatar;
            public AvatarCache.AvatarLateAssign lateAssign;
            public TextMeshProUGUI nameText;
            public Image btnImg;
            public TextMeshProUGUI btnText;
            public ulong id;
            public string friendName;

            public void Bind(ulong newId, string newName)
            {
                bool same = newId == id;
                id = newId;
                friendName = newName;

                // Rows are pooled and may have been hidden by a previous filter
                // (SetActive(false)); a hidden row is invisible even though its
                // data is correct. Always make it visible again here.
                if (go != null) go.SetActive(true);

                nameText.text = newName;

                // Re-init the avatar if the friend changed OR the texture is still
                // blank (the row may have been hidden while its download finished,
                // so the onLoaded write went to an inactive RawImage). This is what
                // makes avatars fill without typing.
                if (!same || avatar.texture == null)
                {
                    // Reset the button to its idle state too.
                    btnText.text = "Invite";
                    btnImg.color = new Color(0.2f, 0.45f, 0.85f, 0f);
                    lateAssign.Init(id, avatar);
                }
            }
        }

        // voice indicators (SteamId -> player replica on this machine)
        readonly Dictionary<ulong, IVoiceMember> voiceBySteamId = new Dictionary<ulong, IVoiceMember>();
        readonly List<IVoiceMember> allVoicePlayers = new List<IVoiceMember>();
        ulong mySteamId;

        float nextStateRefresh;
        int lastCatalogVersion = -1;

        void Awake()
        {
            if (lobbyManager == null) lobbyManager = FindFirstObjectByType<SteamLobbyManager>();
            if (networkManager == null) networkManager = FindFirstObjectByType<ImasuNetworkManager>();
            if (steamManager == null) steamManager = FindFirstObjectByType<SteamManager>();

            BuildUi();
            RefreshScreens();
        }

        void OnEnable()
        {
            if (lobbyManager != null)
            {
                lobbyManager.onLobbyCreated.AddListener(OnLobbyChanged);
                lobbyManager.onLobbyJoined.AddListener(OnLobbyChanged);
                lobbyManager.onLobbyLeft.AddListener(OnLobbyChanged);
                lobbyManager.onLobbyMemberCountChanged.AddListener(OnLobbyChanged);
                lobbyManager.onJoinFailed.AddListener(OnJoinFailed);
            }
        }

        void OnDisable()
        {
            if (lobbyManager != null)
            {
                lobbyManager.onLobbyCreated.RemoveListener(OnLobbyChanged);
                lobbyManager.onLobbyJoined.RemoveListener(OnLobbyChanged);
                lobbyManager.onLobbyLeft.RemoveListener(OnLobbyChanged);
                lobbyManager.onLobbyMemberCountChanged.RemoveListener(OnLobbyChanged);
                lobbyManager.onJoinFailed.RemoveListener(OnJoinFailed);
            }
        }

        void Update()
        {
            // Menus must always have a usable cursor, no matter what locked or
            // hid it earlier (e.g., leaving a match with stale cursor state).
            bool menuShowing = (menuScreen != null && menuScreen.activeSelf) ||
                               (joinScreen != null && joinScreen.activeSelf) ||
                               (setupScreen != null && setupScreen.activeSelf) ||
                               (waitingScreen != null && waitingScreen.activeSelf);
            if (menuShowing)
            {
                if (!Cursor.visible) Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            }

            HandleEsc();

            // Chat log refreshes only when a new message lands (cheap check).
            RefreshChat();

            // Cheap refresh a few times a second to keep labels honest.
            if (Time.unscaledTime >= nextStateRefresh)
            {
                nextStateRefresh = Time.unscaledTime + 0.4f;
                RefreshScreens();
            }
        }

        /// <summary>
        /// ESC closes the top-most MODAL only (invite overlay, then dropdown
        /// popups). It never navigates between screens/scenes — that stays
        /// button-driven per your call.
        /// </summary>
        void HandleEsc()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            // Settings owns ESC while it's open (it stamps EscapeConsumedFrame).
            if (Time.frameCount == SettingsMenu.EscapeConsumedFrame) return;

            if (inviteScreen != null && inviteScreen.activeSelf)
            {
                HideInvite();
                return;
            }
            SimpleDropdown.ClosePopups();
        }

        /// <summary>True when a config problem or the kill-switch blocks sessions; sets a reason.</summary>
        bool AddonBlocked(out string reason)
        {
            if (ImasuSteamworksMaster.Misconfigured)
            {
                reason = "Imasu Steamworks is misconfigured — open the Control Center and wire a config.";
                return true;
            }
            if (!ImasuSteamworksMaster.AddonEnabled)
            {
                reason = "Imasu Steamworks is disabled (kill-switch).";
                return true;
            }
            reason = null;
            return false;
        }

        // ------------------------------------------------------------------ //
        //  Screen state machine
        // ------------------------------------------------------------------ //

        void RefreshScreens()
        {
            if (menuScreen == null || joinScreen == null || setupScreen == null || waitingScreen == null) return;

            // Hide the whole session UI once the match scene is active; the
            // pause menu (ImasuPauseMenu) owns the in-match overlay.
            bool inMatch = networkManager != null &&
                           !string.IsNullOrEmpty(networkManager.matchScene) &&
                           SceneManager.GetActiveScene().name == networkManager.matchScene;
            if (inMatch)
            {
                browsing = false;
                menuScreen.SetActive(false);
                joinScreen.SetActive(false);
                setupScreen.SetActive(false);
                waitingScreen.SetActive(false);
                if (inviteScreen != null) inviteScreen.SetActive(false);
                return;
            }

            bool inSession = NetworkServer.active || NetworkClient.active;
            bool showWaiting = inSession;
            bool showMenu = !inSession;

            menuScreen.SetActive(showMenu && !setupScreen.activeSelf && !joinScreen.activeSelf);
            joinScreen.SetActive(!inSession && joinScreen.activeSelf);
            setupScreen.SetActive(!inSession && setupScreen.activeSelf);
            waitingScreen.SetActive(showWaiting);

            // The Host Setup map/gamemode dropdowns snapshot the catalog at
            // build time. If the Active Config was re-applied (Control Center →
            // Apply, or a new map added), the catalog version bumps — rebuild the
            // options once so the new map shows without leaving Play Mode.
            if (ImasuSteamworksMaster.CatalogVersion != lastCatalogVersion)
            {
                lastCatalogVersion = ImasuSteamworksMaster.CatalogVersion;
                if (setupMode != null)
                {
                    setupMode.options = ModeOptions();
                    setupScene.options = MapOptionsFor(ModeValue());
                    setupScene.Set(0);
                    RefreshSetupEffectiveDefaults();
                }
            }

            if (showWaiting) RefreshWaiting();
            RefreshMenuStatus();
        }

        void ShowMenu()
        {
            browsing = false;
            menuScreen.SetActive(true);
            joinScreen.SetActive(false);
            setupScreen.SetActive(false);
            waitingScreen.SetActive(false);
        }

        void ShowSetup()
        {
            menuScreen.SetActive(false);
            joinScreen.SetActive(false);
            setupScreen.SetActive(true);
            waitingScreen.SetActive(false);
        }

        void ShowJoin()
        {
            menuScreen.SetActive(false);
            joinScreen.SetActive(true);
            setupScreen.SetActive(false);
            waitingScreen.SetActive(false);
            ShowCodeView();
            if (joinStatusLabel != null) joinStatusLabel.text = "";
        }

        // ------------------------------------------------------------------ //
        //  Menu
        // ------------------------------------------------------------------ //

        void RefreshMenuStatus()
        {
            if (menuSteamLabel == null) return;

            // Addon banner first — a missing config is the loudest problem.
            if (ImasuSteamworksMaster.Misconfigured)
            {
                ShowBanner("Imasu Steamworks is misconfigured — open Imasu → Steamworks Control Center and wire a config.",
                    new Color(1f, 0.45f, 0.4f, 1f));
            }
            else if (!ImasuSteamworksMaster.AddonEnabled)
            {
                ShowBanner("Imasu Steamworks is disabled (kill-switch) — the game runs without the addon.",
                    new Color(1f, 0.85f, 0.4f, 1f));
            }
            else
            {
                ShowBanner("", Color.white);
            }

            bool steamOk = steamManager != null && steamManager.initialized;
            menuSteamLabel.text = steamOk
                ? $"Steam: {SteamClient.Name}"
                : "Steam: not initialized";

            if (menuStatusLabel != null)
                menuStatusLabel.text = lobbyManager != null && lobbyManager.inLobby
                    ? "In a lobby."
                    : "Not in a lobby.";
        }

        void ShowBanner(string text, Color color)
        {
            if (addonBanner == null) return;
            if (string.IsNullOrEmpty(text))
            {
                addonBanner.gameObject.SetActive(false);
                return;
            }
            addonBanner.text = text;
            addonBanner.color = color;
            addonBanner.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ //
        //  Waiting room
        // ------------------------------------------------------------------ //

        void RefreshWaiting()
        {
            if (waitingScreen == null || lobbyManager == null) return;
            bool codeJustCopied = Time.unscaledTime - lastCodeCopyTime < 1.2f;
            if (lobbyManager.inLobby)
            {
                if (lobbyCodeField != null)
                {
                    // Short friendly code when available; raw ID for legacy lobbies.
                    string code = lobbyManager.settings.code;
                    lobbyCodeField.text = codeJustCopied
                        ? "Copied!"
                        : (string.IsNullOrEmpty(code) ? lobbyManager.currentLobby.Id.ToString() : code);
                }

                if (waitingLobbyInfo != null)
                    waitingLobbyInfo.text = $"{lobbyManager.settings.lobbyName}  ({lobbyManager.members.Count}/{lobbyManager.settings.maxPlayers})";
            }
            else
            {
                if (lobbyCodeField != null) lobbyCodeField.text = codeJustCopied ? "Copied!" : "-";
                if (waitingLobbyInfo != null) waitingLobbyInfo.text = "Local session";
            }

            CacheVoicePlayers();

            if (inviteButton != null) inviteButton.SetActive(lobbyManager.inLobby);

            foreach (Transform child in memberList)
                Destroy(child.gameObject);

            int i = 0;
            if (lobbyManager.inLobby)
            {
                foreach (KeyValuePair<SteamId, string> kv in lobbyManager.members)
                {
                    voiceBySteamId.TryGetValue(kv.Key.Value, out IVoiceMember vp);
                    AddMemberRow(i, RowText(vp, kv.Value, kv.Key == lobbyManager.currentLobby.Owner.Id),
                        RowColor(vp), kv.Key.Value);
                    i++;
                }
            }
            else
            {
                // Local (KCP) session: no Steam lobby, so list the networked players.
                foreach (IVoiceMember vp in allVoicePlayers)
                {
                    AddMemberRow(i, RowText(vp, vp.DisplayName, false), RowColor(vp), vp.OwnerSteamId);
                    i++;
                }
            }

            // Voice capture piggybacks the Steam client (mic APIs), so it can only
            // work when Steam is initialized — hide the control otherwise.
            bool steamOk = steamManager != null && steamManager.initialized;
            if (pushToTalkButton != null) pushToTalkButton.SetActive(steamOk);

            if (voiceStatsLabel != null)
            {
                if (!steamOk)
                {
                    voiceStatsLabel.text = "Voice requires Steam.";
                }
                else
                {
                    ulong sent = 0, recv = 0;
                    foreach (IVoiceMember vp in allVoicePlayers)
                    {
                        if (vp.IsLocalPlayer) sent += (ulong)vp.BytesSent;
                        recv += (ulong)vp.BytesReceived;
                    }
                    voiceStatsLabel.text = $"Voice: sent {sent / 1024f:0.#} KB · received {recv / 1024f:0.#} KB";
                }
            }

            bool isHost = NetworkServer.active;
            if (startMatchButton != null) startMatchButton.SetActive(isHost);

            if (waitingStatusLabel != null)
                waitingStatusLabel.text = isHost
                    ? "You are the host. Press Start Match when everyone is ready."
                    : "Waiting for the host to start the match.";
        }

        void CacheVoicePlayers()
        {
            mySteamId = 0;
            if (steamManager != null && steamManager.initialized)
            {
                try { if (SteamClient.IsValid) mySteamId = SteamClient.SteamId.Value; } catch { }
            }

            allVoicePlayers.Clear();
            voiceBySteamId.Clear();
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IVoiceMember vp)) continue;
                allVoicePlayers.Add(vp);
                if (vp.OwnerSteamId != 0 && !voiceBySteamId.ContainsKey(vp.OwnerSteamId))
                    voiceBySteamId[vp.OwnerSteamId] = vp;
            }
        }

        static string RowText(IVoiceMember vp, string name, bool isHost)
        {
            string hostTag = isHost ? "  (host)" : "";
            if (vp == null) return $"  {name}{hostTag}";

            string you = vp.IsLocalPlayer ? " (you)" : "";
            string voice = vp.IsLocalPlayer
                ? (vp.MicLive ? "  [mic live]" : "")
                : (vp.Speaking ? "  [talking]" : "");
            return $"  {name}{you}{voice}{hostTag}";
        }

        static Color RowColor(IVoiceMember vp)
        {
            if (vp != null && vp.IsLocalPlayer && vp.MicLive)
                return new Color(0.35f, 0.9f, 1f);      // cyan: your mic is live
            if (vp != null && vp.Speaking)
                return new Color(0.4f, 1f, 0.45f);      // green: talking
            return new Color(0.72f, 0.76f, 0.84f);      // idle
        }

        /// <summary>
        /// One waiting-room member row: square Steam avatar on the left (appears
        /// once fetched), name/voice-state text beside it.
        /// </summary>
        void AddMemberRow(int index, string text, Color color, ulong steamId)
        {
            GameObject row = new GameObject("MemberRow", typeof(RectTransform));
            row.transform.SetParent(memberList, false);

            Image bg = row.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.03f);
            bg.raycastTarget = false;

            GameObject avGo = new GameObject("Avatar", typeof(RectTransform));
            avGo.transform.SetParent(row.transform, false);
            RawImage ri = avGo.AddComponent<RawImage>();
            ri.raycastTarget = false;
            RectTransform avRt = (RectTransform)avGo.transform;
            avRt.anchorMin = new Vector2(0, 0.5f);
            avRt.anchorMax = new Vector2(0, 0.5f);
            avRt.pivot = new Vector2(0, 0.5f);
            avRt.sizeDelta = new Vector2(26, 26);
            avRt.anchoredPosition = new Vector2(4, 0);
            if (steamId != 0)
                avGo.AddComponent<AvatarCache.AvatarLateAssign>().Init(steamId, ri);

            GameObject txtGo = new GameObject("Name", typeof(RectTransform));
            txtGo.transform.SetParent(row.transform, false);
            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            RectTransform trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(38, 1);
            trt.offsetMax = new Vector2(-4, -1);

            RectTransform rowRt = (RectTransform)row.transform;
            float top = 1f - index * 0.12f;
            rowRt.anchorMin = new Vector2(0, top - 0.1f);
            rowRt.anchorMax = new Vector2(1, top);
            rowRt.pivot = new Vector2(0, 1);
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        void OnCreateLobbyClicked()
        {
            if (AddonBlocked(out string reason))
            {
                ShowBanner(reason, new Color(1f, 0.45f, 0.4f, 1f));
                return;
            }

            int modeIndex = ModeValue();
            int mapIndex = setupScene != null ? setupScene.value : 0;

            // No Steam mode: skip the lobby entirely and run a plain KCP host.
            if (!GameSettings.UseSteam)
            {
                Debug.Log("[ImasuSessionUI] Hosting LAN game (no Steam).");
                // Remember the host's map choice so Start Match has a scene to load.
                if (networkManager != null && setupScene != null)
                    networkManager.matchScene = networkManager.MapSceneAt(modeIndex, setupScene.value);
                networkManager?.StartLocalHost();
                ShowMenu(); // waiting room takes over once in-session
                return;
            }

            LobbySettings s = LobbySettings.Defaults(modeIndex, mapIndex);
            if (!string.IsNullOrEmpty(setupName.text)) s.lobbyName = setupName.text;
            if (int.TryParse(setupMaxPlayers.text, out int mp)) s.maxPlayers = Mathf.Clamp(mp, 2, s.maxPlayers);
            if (setupScene != null)
                s.sceneName = networkManager != null
                    ? networkManager.MapSceneAt(modeIndex, setupScene.value)
                    : SceneFromConfigFallback(modeIndex);
            s.gamemode = ModeName();
            if (float.TryParse(setupVoiceRange.text, out float vr)) s.voiceRange = vr;
            if (setupSecurity != null)
                s.security = (LobbySecurity)Mathf.Clamp(setupSecurity.value, 0, (int)LobbySecurity.Open);
            s.isPublic = s.security == LobbySecurity.Open;
            s.modeIndex = modeIndex;
            s.mapIndex = mapIndex;

            lobbyManager?.CreateLobby(s);
            ShowMenu(); // waiting room takes over once in-session
        }

        int ModeValue() => setupMode != null ? setupMode.value : 0;

        string ModeName()
        {
            if (setupMode == null) return "";
            string[] names = networkManager != null ? networkManager.ModeDisplayNames : null;
            return names != null && names.Length > 0 ? names[setupMode.value] : "";
        }

        string SceneFromConfigFallback(int modeIndex)
        {
            // No networkManager wired: resolve from the config directly.
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            return cfg.SceneFor(modeIndex, setupScene != null ? setupScene.value : 0)
                ?? cfg.FirstMapScene() ?? "Game";
        }

        void OnJoinByCodeClicked()
        {
            if (joinStatusLabel != null) joinStatusLabel.text = "";

            if (AddonBlocked(out string reason))
            {
                if (joinStatusLabel != null) joinStatusLabel.text = reason;
                return;
            }

            string input = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
            string normalized = LobbyCode.Normalize(input);

            // Address path: IP / hostname (with optional port). Codes never
            // contain '.' or ':'.
            if (normalized.IndexOf('.') >= 0 || normalized.IndexOf(':') >= 0 ||
                string.Equals(normalized, "localhost", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!ImasuSteamworksMaster.Config.allowNonSteamServers)
                {
                    if (joinStatusLabel != null)
                        joinStatusLabel.text = "Non-Steam (LAN) servers are disabled by configuration.";
                    return;
                }
                // Strip any ':port' for the address field (KCP uses configured port).
                string host = normalized;
                int colon = host.IndexOf(':');
                if (colon > 0) host = host.Substring(0, colon);
                networkManager?.StartLanClient(host);
                return;
            }

            // Steam code / raw lobby ID path.
            if (LobbyCode.LooksLikeCode(normalized) || LobbyCode.LooksLikeRawLobbyId(normalized))
            {
                lobbyManager?.JoinByCode(normalized);
                return;
            }

            if (joinStatusLabel != null)
                joinStatusLabel.text = "Enter a 7-character code or an IP address.";
        }

        void OnJoinFailed(string reason)
        {
            // Only meaningful while the join screen is up; other screens have
            // their own feedback (waiting room takes over on success).
            if (joinScreen == null || !joinScreen.activeSelf) return;
            if (joinStatusLabel != null) joinStatusLabel.text = reason;
        }

        void OnCopyLobbyCode()
        {
            if (lobbyCodeField != null && !string.IsNullOrEmpty(lobbyCodeField.text))
            {
                GUIUtility.systemCopyBuffer = lobbyCodeField.text;
                lastCodeCopyTime = Time.unscaledTime;
            }
        }

        void OnStartMatch()
        {
            if (AddonBlocked(out string reason))
            {
                waitingStatusLabel.text = reason;
                return;
            }

            // Steam lobby: StartMatch reads the chosen map from lobby settings.
            if (lobbyManager != null && lobbyManager.inLobby)
            {
                int modeIndex = lobbyManager.settings.modeIndex >= 0
                    ? lobbyManager.settings.modeIndex
                    : ImasuSteamworksMaster.Config.ModeIndexByName(lobbyManager.settings.gamemode);
                int min = ImasuSteamworksMaster.Config.EffectiveMinPlayers(modeIndex);
                if (lobbyManager.members.Count < min)
                {
                    waitingStatusLabel.text = $"Need {min} players in the lobby to start (have {lobbyManager.members.Count}).";
                    return;
                }
                if (lobbyManager.StartMatch()) return;
                waitingStatusLabel.text = "Could not start the match.";
                return;
            }

            // Non-Steam host: matchScene was set at create time.
            if (NetworkServer.active)
            {
                int min = ImasuSteamworksMaster.Config.EffectiveMinPlayers();
                int connected = 0;
                foreach (var c in NetworkServer.connections.Values)
                    if (c.identity != null) connected++;
                if (connected < min)
                {
                    waitingStatusLabel.text = $"Need {min} players to start (have {connected}).";
                    return;
                }
                if (networkManager != null && networkManager.StartMatch()) return;
                waitingStatusLabel.text = "Could not start the match.";
                return;
            }

            Debug.LogWarning("[ImasuSessionUI] Start Match needs an active host.");
        }

        void OnLeaveLobby()
        {
            lobbyManager?.LeaveLobby();
            HideInvite();
            ShowMenu();
        }

        void OnQuit()
        {
            Application.Quit();
        }

        void OnLobbyChanged(int _)
        {
            RefreshScreens();
        }

        void OnLobbyChanged()
        {
            RefreshScreens();
        }

        // ------------------------------------------------------------------ //
        //  UI construction
        // ------------------------------------------------------------------ //

        void BuildUi()
        {
            GameObject uiRoot = new GameObject("SessionUI");
            uiRoot.transform.SetParent(transform, false);

            canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            uiRoot.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            menuScreen = BuildMenuScreen(canvas.transform);
            joinScreen = BuildJoinScreen(canvas.transform);
            setupScreen = BuildSetupScreen(canvas.transform);
            waitingScreen = BuildWaitingScreen(canvas.transform);
            inviteScreen = BuildInviteScreen(canvas.transform); // created last: draws on top

            // Screens are born active; make Main Menu the explicit boot screen.
            menuScreen.SetActive(true);
            joinScreen.SetActive(false);
            setupScreen.SetActive(false);
            waitingScreen.SetActive(false);
        }

        GameObject BuildMenuScreen(Transform parent)
        {
            GameObject screen = Panel(parent, "MainMenu");
            Title(screen.transform, "Hidden");

            menuSteamLabel = Label(screen.transform, "Steam: checking...", new Vector2(0.02f, 0.94f), TextAnchor.MiddleLeft);
            menuStatusLabel = Label(screen.transform, "Not in a lobby.", new Vector2(0.02f, 0.9f), TextAnchor.MiddleLeft);

            // Addon banner: hidden normally; shows a hard-error (red) when the
            // config is missing/misconfigured, or a soft note (yellow) when the
            // addon kill-switch is off.
            addonBanner = Label(screen.transform, "", new Vector2(0.5f, 0.78f), TextAnchor.MiddleCenter);
            addonBanner.fontSize = 14;
            addonBanner.color = Color.white;
            addonBanner.gameObject.SetActive(false);

            // Standard main menu. All screens live in this one scene so a 3D
            // backdrop can later sit behind every panel.
            Btn(screen.transform, "Join Game", 0.72f, ShowJoin);
            Btn(screen.transform, "Host Game", 0.60f, ShowSetup);
            Btn(screen.transform, "Settings", 0.48f, () => SettingsMenu.Open());
            Btn(screen.transform, "Quit", 0.36f, OnQuit);

            return screen;
        }

        GameObject BuildJoinScreen(Transform parent)
        {
            GameObject screen = Panel(parent, "JoinGame");
            Title(screen.transform, "Join Game");

            // --- Code entry (default view) ---
            codeView = new GameObject("CodeView", typeof(RectTransform));
            codeView.transform.SetParent(screen.transform, false);
            Layout(codeView.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            Label(codeView.transform, "Enter Lobby Code:", new Vector2(0.5f, 0.80f), TextAnchor.MiddleCenter);
            joinCodeInput = Input(codeView.transform, "e.g. HG6FY14", new Vector2(0.5f, 0.72f));
            joinCodeInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -8);

            Btn(codeView.transform, "Join", 0.62f, OnJoinByCodeClicked);

            joinStatusLabel = Label(codeView.transform, "", new Vector2(0.5f, 0.54f), TextAnchor.MiddleCenter);
            joinStatusLabel.fontSize = 15;
            joinStatusLabel.color = new Color(1f, 0.5f, 0.4f, 1f);

            Btn(codeView.transform, "Browse Servers", 0.42f, ShowBrowserView);
            Btn(codeView.transform, "Back", 0.10f, ShowMenu);

            // --- Server browser (CS:GO style list of public lobbies) ---
            browserView = new GameObject("BrowserView", typeof(RectTransform));
            browserView.transform.SetParent(screen.transform, false);
            Layout(browserView.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            Label(browserView.transform, "Filter:", new Vector2(0.03f, 0.87f), TextAnchor.MiddleLeft);
            browserFilter = Input(browserView.transform, "Search servers...", new Vector2(0.10f, 0.84f));

            Btn(browserView.transform, "Refresh", 0.84f, RefreshBrowser, out GameObject refreshBtn);
            ((RectTransform)refreshBtn.transform).anchoredPosition += new Vector2(470, 0);
            ((RectTransform)refreshBtn.transform).sizeDelta = new Vector2(160, 40);

            BuildBrowserHeader();

            browserContent = ScrollView(browserView.transform, new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.74f));

            browserStatusLabel = Label(browserView.transform, "Press Refresh to search.", new Vector2(0.03f, 0.085f), TextAnchor.MiddleLeft);
            browserStatusLabel.fontSize = 14;

            Btn(browserView.transform, "Back to Code Entry", 0.02f, ShowCodeView);

            return screen;
        }

        void ShowBrowserView()
        {
            browsing = true;
            codeView.SetActive(false);
            browserView.SetActive(true);
            if (browserFilter != null) browserFilter.text = "";
            foreach (Transform child in browserContent)
                Destroy(child.gameObject);
            if (browserStatusLabel != null) browserStatusLabel.text = "Press Refresh to search.";

            // Ask the LAN for hosts while we're on this view (addon present only).
            ILanDiscovery lan = LanDiscoveryQuery.Find();
            if (lan != null)
            {
                lan.Clear();
                lan.StartDiscovery();
            }
        }

        void ShowCodeView()
        {
            browsing = false;
            browserView.SetActive(false);
            codeView.SetActive(true);
            LanDiscoveryQuery.Find()?.StopDiscovery();
        }

        /// <summary>Column header row matching BrowserRow widths so they align.</summary>
        void BuildBrowserHeader()
        {
            GameObject header = new GameObject("BrowserHeader", typeof(RectTransform));
            header.transform.SetParent(browserView.transform, false);
            RectTransform rt = (RectTransform)header.transform;
            Layout(rt, new Vector2(0.03f, 0.76f), new Vector2(0.97f, 0.80f));

            HorizontalLayoutGroup hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 2, 2);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            AddCell(header.transform, "Server Name", 0, true);
            AddCell(header.transform, "Players", 90, true);
            AddCell(header.transform, "Map", 150, true);
            AddCell(header.transform, "Mode", 110, true);
            AddCell(header.transform, "", 70, true); // Join column spacer
        }

        async void RefreshBrowser()
        {
            if (!browsing)
            {
                if (browserStatusLabel != null) browserStatusLabel.text = "";
                return;
            }

            bool steamOk = steamManager != null && steamManager.initialized;
            if (browserStatusLabel != null)
                browserStatusLabel.text = steamOk ? "Searching..." : "Searching LAN only...";

            // LAN discovery results are polled from the addon (if installed);
            // shown even when Steam is unavailable.
            PopulateBrowser(System.Array.Empty<global::Steamworks.Data.Lobby>());

            if (!steamOk) return;

            try
            {
                global::Steamworks.Data.Lobby[] lobbies = await global::Steamworks.SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithSlotsAvailable(1)
                    // Only OUR lobbies: other games share the Spacewar appid.
                    .WithKeyValue(LobbySettings.DataMarker, LobbySettings.MarkerValue)
                    .WithMaxResults(50)
                    .RequestAsync();

                if (!browsing) return; // user navigated away while searching
                PopulateBrowser(lobbies ?? System.Array.Empty<global::Steamworks.Data.Lobby>());
            }
            catch (System.Exception e)
            {
                if (!browsing) return;
                if (browserStatusLabel != null) browserStatusLabel.text = $"Search failed: {e.Message}";
                Debug.LogWarning($"[ImasuSessionUI] Browser query failed: {e}");
            }
        }

        void PopulateBrowser(global::Steamworks.Data.Lobby[] lobbies)
        {
            foreach (Transform child in browserContent)
                Destroy(child.gameObject);

            string filter = browserFilter != null ? browserFilter.text.Trim() : "";
            bool steamShown = false;

            foreach (global::Steamworks.Data.Lobby lobby in lobbies)
            {
                string name = lobby.GetData(LobbySettings.DataName);
                if (string.IsNullOrEmpty(name)) name = $"Unnamed server ({lobby.Id})";
                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string scene = lobby.GetData(LobbySettings.DataScene);
                string map = networkManager != null ? networkManager.MapDisplayNameFor(scene) : scene;

                int max = lobby.MaxMembers;
                if (int.TryParse(lobby.GetData(LobbySettings.DataMaxPlayers), out int parsedMax) && parsedMax > 0)
                    max = parsedMax;
                string players = $"{lobby.MemberCount} / {max}";

                string mode = lobby.GetData(LobbySettings.DataGameMode);
                if (string.IsNullOrEmpty(mode))
                {
                    // Legacy lobbies: fall back to the security label.
                    if (byte.TryParse(lobby.GetData(LobbySettings.DataSecurity), out byte sec))
                    {
                        mode = sec switch
                        {
                            (byte)LobbySecurity.InviteOnly => "Invite only",
                            (byte)LobbySecurity.FriendsOnly => "Friends",
                            _ => "Open"
                        };
                    }
                    else mode = "?";
                }

                ulong lobbyId = lobby.Id;
                BrowserRow(name, players, map, mode, () => lobbyManager?.JoinLobby((global::Steamworks.SteamId)lobbyId));
                steamShown = true;
            }

            int lanShown = AppendLanRows(filter);

            int total = (steamShown ? lobbies.Length : 0) + lanShown;
            if (browserStatusLabel != null)
                browserStatusLabel.text = total == 0
                    ? (string.IsNullOrEmpty(filter) ? "No servers found." : "No servers match that filter.")
                    : $"{total} server{(total == 1 ? "" : "s")} found.";
        }

        /// <summary>Appends LAN discovery rows after the Steam section; returns count.</summary>
        int AppendLanRows(string filter)
        {
            if (!ImasuSteamworksMaster.Config.allowNonSteamServers) return 0;
            ILanDiscovery lan = LanDiscoveryQuery.Find();
            List<LanServerInfo> lanList = lan != null ? lan.Snapshot() : null;
            if (lanList == null || lanList.Count == 0) return 0;

            // Thin divider between the Steam and LAN sections.
            GameObject divider = new GameObject("LanDivider", typeof(RectTransform));
            divider.transform.SetParent(browserContent, false);
            LayoutElement dLe = divider.AddComponent<LayoutElement>();
            dLe.minHeight = 6;
            dLe.preferredHeight = 6;
            Image dImg = divider.AddComponent<Image>();
            dImg.color = new Color(1f, 1f, 1f, 0.08f);
            dImg.raycastTarget = false;
            AddCellText(divider.transform, "LAN SERVERS", TextAlignmentOptions.Left, 13,
                new Color(1f, 1f, 1f, 0.55f), stretch: true);

            int shown = 0;
            foreach (LanServerInfo server in lanList)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    server.serverName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string host = server.host;
                BrowserRow(server.serverName, $"{server.currentPlayers} / {server.maxPlayers}",
                    server.mapName, "LAN",
                    () =>
                    {
                        if (networkManager != null && !string.IsNullOrEmpty(host))
                            networkManager.StartLanClient(host);
                    });
                shown++;
            }
            return shown;
        }

        static bool TryGetHost(string uri, out string host)
        {
            host = "";
            try
            {
                if (string.IsNullOrEmpty(uri)) return false;
                host = new System.Uri(uri).Host;
                return !string.IsNullOrEmpty(host);
            }
            catch { return false; }
        }

        /// <summary>One server row: fixed-width columns + Join button.</summary>
        void BrowserRow(string name, string players, string map, string mode, System.Action onJoin)
        {
            GameObject row = new GameObject("ServerRow", typeof(RectTransform));
            row.transform.SetParent(browserContent, false);

            Image bg = row.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.04f);
            bg.raycastTarget = false;

            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 30;
            rowLe.preferredHeight = 32;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 3, 3);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            AddCell(row.transform, name, 0, false);
            AddCell(row.transform, players, 90, false);
            AddCell(row.transform, map, 150, false);
            AddCell(row.transform, mode, 110, false);

            GameObject btnGo = new GameObject("JoinButton", typeof(RectTransform));
            btnGo.transform.SetParent(row.transform, false);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.45f, 0.85f, 1);
            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            LayoutElement btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 70;

            AddCellText(btnGo.transform, "Join", TextAlignmentOptions.Center, 14, Color.white, stretch: true);

            btn.onClick.AddListener(() => onJoin?.Invoke());
        }

        /// <summary>Fixed-width column cell (width 0 = fill remaining space).</summary>
        static void AddCell(Transform parent, string text, float width, bool header)
        {
            GameObject go = new GameObject("Cell_" + text.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            if (!string.IsNullOrEmpty(text) || width > 0)
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                if (width > 0) le.preferredWidth = width;
                else le.flexibleWidth = 1;
                le.minWidth = header && width == 0 ? 120 : 40;
            }

            AddCellText(go.transform, text,
                TextAlignmentOptions.Left,
                header ? 14 : 15,
                header ? new Color(1f, 1f, 1f, 0.65f) : Color.white,
                stretch: true);
        }

        static void AddCellText(Transform parent, string text, TextAlignmentOptions align, float size,
            Color color, bool stretch)
        {
            GameObject txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            if (stretch)
            {
                RectTransform trt = (RectTransform)txtGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
            }
        }

        GameObject BuildSetupScreen(Transform parent)
        {
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            GameObject screen = Panel(parent, "GameSetup");
            Title(screen.transform, "Game Setup (host)");

            Label(screen.transform, "Server Name:", new Vector2(0.05f, 0.90f), TextAnchor.MiddleLeft);
            setupName = Input(screen.transform, cfg.EffectiveLobbyName(0), new Vector2(0.05f, 0.855f), cfg.EffectiveLobbyName(0));

            Label(screen.transform, "Max Players:", new Vector2(0.05f, 0.795f), TextAnchor.MiddleLeft);
            setupMaxPlayers = Input(screen.transform, cfg.EffectiveMaxPlayers(0, 0).ToString(), new Vector2(0.05f, 0.75f), cfg.EffectiveMaxPlayers(0, 0).ToString());

            Label(screen.transform, "Gamemode:", new Vector2(0.05f, 0.685f), TextAnchor.MiddleLeft);
            setupMode = Dropdown(screen.transform, ModeOptions(), new Vector2(0.05f, 0.635f));
            setupMode.onValueChanged += _ => { PopulateMapDropdown(); RefreshSetupEffectiveDefaults(); };

            Label(screen.transform, "Map / Scene:", new Vector2(0.05f, 0.58f), TextAnchor.MiddleLeft);
            setupScene = Dropdown(screen.transform, MapOptionsFor(ModeValue()), new Vector2(0.05f, 0.53f));
            setupScene.onValueChanged += _ => RefreshSetupEffectiveDefaults();

            Label(screen.transform, "Voice Max Distance (m):", new Vector2(0.05f, 0.47f), TextAnchor.MiddleLeft);
            setupVoiceRange = Input(screen.transform, cfg.EffectiveVoipMaxDistance(0).ToString("0.#"), new Vector2(0.05f, 0.42f), cfg.EffectiveVoipMaxDistance(0).ToString("0.#"));

            Label(screen.transform, "Security (who can join):", new Vector2(0.05f, 0.36f), TextAnchor.MiddleLeft);
            setupSecurity = Dropdown(screen.transform,
                new[] { "Invite only", "Friends", "Open" }, new Vector2(0.05f, 0.31f), (int)cfg.EffectiveSecurity(0));

            // Networking choice: Steam lobbies+transport, or LAN/KCP (no Steam).
            // Hidden entirely when the debug flag allowNonSteamServers is OFF.
            setupUseSteamCheck = Checkbox(screen.transform, "Use Steam networking",
                new Vector2(0.05f, 0.25f), GameSettings.UseSteam,
                v => GameSettings.UseSteam = v);
            var nonSteamHint = Label(screen.transform, "Off = LAN game (no Steam; friends join by IP or the LAN browser).",
                new Vector2(0.05f, 0.205f), TextAnchor.MiddleLeft, 13, new Color(1f, 1f, 1f, 0.45f));

            if (!cfg.allowNonSteamServers)
            {
                GameSettings.UseSteam = true;
                setupUseSteamCheck.SetActive(false);
                nonSteamHint.gameObject.SetActive(false);
            }

            Btn(screen.transform, "Create Lobby & Host", 0.10f, OnCreateLobbyClicked, out GameObject createBtn);
            Btn(screen.transform, "Back", 0.10f, ShowMenu, out GameObject backBtn);
            ((RectTransform)createBtn.transform).anchoredPosition += new Vector2(-180, 0);
            ((RectTransform)backBtn.transform).anchoredPosition += new Vector2(180, 0);

            return screen;
        }

        /// <summary>
        /// When the gamemode or map changes, refresh the Max Players / Voice Range
        /// fields to the mode/map's effective defaults and clamp Max Players to
        /// that mode's cap (a smaller map shouldn't overflow its spawner count).
        /// </summary>
        void RefreshSetupEffectiveDefaults()
        {
            if (setupMaxPlayers == null || setupMode == null) return;

            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            int modeIndex = ModeValue();
            int mapIndex = setupScene != null ? setupScene.value : 0;

            int cap = cfg.EffectiveMaxPlayers(modeIndex, mapIndex);
            if (int.TryParse(setupMaxPlayers.text, out int current))
                setupMaxPlayers.text = Mathf.Clamp(current, 2, cap).ToString();
            else
                setupMaxPlayers.text = cap.ToString();

            if (setupVoiceRange != null)
                setupVoiceRange.text = cfg.EffectiveVoipMaxDistance(modeIndex).ToString("0.#");

            if (setupSecurity != null)
                setupSecurity.Set((int)cfg.EffectiveSecurity(modeIndex));
        }

        /// <summary>Gamemode dropdown options from the manager's catalog (config-driven).</summary>
        string[] ModeOptions()
        {
            if (networkManager != null)
            {
                string[] names = networkManager.ModeDisplayNames;
                if (names != null && names.Length > 0) return names;
            }
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            if (cfg.HasGameModes) return cfg.ModeDisplayNames;
            return new[] { "Default" };
        }

        /// <summary>Map dropdown options for the selected gamemode (config-driven).</summary>
        string[] MapOptionsFor(int modeIndex)
        {
            if (networkManager != null)
            {
                string[] names = networkManager.MapDisplayNamesForMode(modeIndex);
                if (names != null && names.Length > 0) return names;
            }
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            string[] cfgNames = cfg.MapDisplayNamesForMode(modeIndex);
            if (cfgNames != null && cfgNames.Length > 0) return cfgNames;
            return availableScenes.Length > 0 ? availableScenes : new[] { "Default" };
        }

        /// <summary>Rebuild the map dropdown when the gamemode changes.</summary>
        void PopulateMapDropdown()
        {
            if (setupScene == null) return;
            setupScene.options = MapOptionsFor(ModeValue());
            setupScene.Set(0);
        }

        GameObject BuildWaitingScreen(Transform parent)
        {
            GameObject screen = Panel(parent, "WaitingRoom");
            Title(screen.transform, "Waiting Room");

            waitingLobbyInfo = Label(screen.transform, "Lobby: -", new Vector2(0.02f, 0.92f), TextAnchor.MiddleLeft);

            Label(screen.transform, "Lobby code (click to copy):", new Vector2(0.02f, 0.84f), TextAnchor.MiddleLeft);
            lobbyCodeField = CodeLabel(screen.transform, new Vector2(0.02f, 0.76f));

            Label(screen.transform, "Players:", new Vector2(0.02f, 0.56f), TextAnchor.MiddleLeft);
            memberList = new GameObject("MemberList", typeof(RectTransform)).GetComponent<RectTransform>();
            memberList.SetParent(screen.transform, false);
            // Keep clear of the action buttons on the right (rows now have
            // visible backgrounds + avatars, so overlap would show).
            Layout(memberList, new Vector2(0.02f, 0.1f), new Vector2(0.34f, 0.5f));

            Btn(screen.transform, "Start Match", 0.6f, OnStartMatch, out startMatchButton);
            Btn(screen.transform, "Push to Talk", 0.48f, OnPushToTalk, out pushToTalkButton);
            Btn(screen.transform, "Leave Lobby", 0.36f, OnLeaveLobby, out GameObject leaveBtn);
            Btn(screen.transform, "Invite Friends", 0.24f, OnInviteFriends, out inviteButton);

            // Shift the action column right of the member list.
            foreach (GameObject b in new[] { startMatchButton, pushToTalkButton, leaveBtn, inviteButton })
                ((RectTransform)b.transform).anchoredPosition += new Vector2(170, 0);

            waitingStatusLabel = Label(screen.transform, "", new Vector2(0.5f, 0.13f), TextAnchor.MiddleLeft);
            voiceStatsLabel = Label(screen.transform, "Voice: -", new Vector2(0.02f, 0.02f), TextAnchor.MiddleLeft);

            BuildChatPanel(screen.transform);

            return screen;
        }

        // ------------------------------------------------------------------ //
        //  Waiting-room chat
        // ------------------------------------------------------------------ //

        void BuildChatPanel(Transform parent)
        {
            GameObject panel = new GameObject("ChatPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.35f);
            Layout((RectTransform)panel.transform, new Vector2(0.78f, 0.05f), new Vector2(0.98f, 0.55f));

            Label(panel.transform, "Chat", new Vector2(0.02f, 0.94f), TextAnchor.MiddleLeft);

            chatLog = ScrollView(panel.transform, new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.90f));
            chatScroll = chatLog.GetComponentInParent<ScrollRect>();

            chatHintLabel = Label(panel.transform, "", new Vector2(0.05f, 0.5f), TextAnchor.MiddleLeft,
                13, new Color(1f, 1f, 1f, 0.6f));

            chatInput = Input(panel.transform, "Say something...", new Vector2(0.02f, 0.06f));
            Layout((RectTransform)chatInput.transform, new Vector2(0.02f, 0.02f), new Vector2(0.78f, 0.10f));
            chatInput.onSubmit.AddListener(_ => OnSendChat());

            Btn(panel.transform, "Send", 0.5f, OnSendChat, out chatSendButton);
            Layout((RectTransform)chatSendButton.transform, new Vector2(0.80f, 0.02f), new Vector2(0.98f, 0.10f));
        }

        void OnSendChat()
        {
            if (chatInput == null || string.IsNullOrEmpty(chatInput.text)) return;
            if (LobbyChat.instance == null) return;
            LobbyChat.instance.Send(chatInput.text);
            chatInput.text = "";
            chatInput.ActivateInputField();
        }

        /// <summary>Keeps the chat panel honest: hides/uns the controls when there is
        /// no LobbyChat object yet, and rebuilds the log only when a new entry lands.</summary>
        void RefreshChat()
        {
            if (chatLog == null) return;

            LobbyChat chat = LobbyChat.instance;
            if (chat == null)
            {
                if (chatHintLabel != null) chatHintLabel.text = "Chat is available once connected.";
                if (chatInput != null) chatInput.interactable = false;
                if (chatSendButton != null) chatSendButton.SetActive(false);
                return;
            }

            if (chatHintLabel != null) chatHintLabel.text = "";
            if (chatInput != null && !chatInput.interactable) chatInput.interactable = true;
            if (chatSendButton != null && !chatSendButton.activeSelf) chatSendButton.SetActive(true);

            if (chat.entries.Count == lastChatCount) return;
            lastChatCount = chat.entries.Count;

            foreach (Transform child in chatLog) Destroy(child.gameObject);

            int start = Mathf.Max(0, chat.entries.Count - 30);
            for (int i = start; i < chat.entries.Count; i++)
            {
                ChatEntry e = chat.entries[i];
                GameObject row = new GameObject("ChatRow", typeof(RectTransform));
                row.transform.SetParent(chatLog, false);
                TextMeshProUGUI t = row.AddComponent<TextMeshProUGUI>();
                t.fontSize = 14;
                t.enableWordWrapping = true;
                t.raycastTarget = false;
                t.color = new Color(0.88f, 0.9f, 0.94f);
                t.text = e.isSystem
                    ? $"<color=#888888>» {e.text}</color>"
                    : $"<color=#7db4ff><b>{e.name}:</b></color> {e.text}";
            }

            if (chatScroll != null) chatScroll.verticalNormalizedPosition = 0f;
        }

        bool pushToTalkHeld;

        void OnPushToTalk()
        {
            pushToTalkHeld = !pushToTalkHeld;
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVoiceMember vp) { vp.SetPushToTalk(pushToTalkHeld); break; }
        }

        // ------------------------------------------------------------------ //
        //  Invite friends (custom list + native overlay picker)
        // ------------------------------------------------------------------ //

        void OnInviteFriends()
        {
            if (lobbyManager == null || !lobbyManager.inLobby)
            {
                Debug.LogWarning("[ImasuSessionUI] Invites need an active Steam lobby.");
                return;
            }

            inviteScreen.SetActive(true);
            if (inviteSearch != null) inviteSearch.text = "";
            RefreshInviteList();
        }

        void HideInvite()
        {
            if (inviteScreen != null) inviteScreen.SetActive(false);
        }

        void OnOverlayInvite()
        {
            if (lobbyManager == null || !lobbyManager.inLobby) return;
            try
            {
                SteamFriends.OpenGameInviteOverlay(lobbyManager.currentLobby.Id);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ImasuSessionUI] Overlay invite failed: {e.Message}");
            }
        }

        void RefreshInviteList()
        {
            if (inviteContent == null) return;

            if (steamManager == null || !steamManager.initialized)
            {
                HideAllRows();
                if (inviteStatusLabel != null) inviteStatusLabel.text = "Steam is not initialized.";
                return;
            }

            string filter = inviteSearch != null ? inviteSearch.text.Trim() : "";

            // Gather the desired friends (filtered, online, capped).
            const int MaxRows = 50;
            List<KeyValuePair<ulong, string>> desired = new List<KeyValuePair<ulong, string>>();
            int totalOnline = 0;
            foreach (global::Steamworks.Friend friend in SteamFriends.GetFriends())
            {
                if (!friend.IsOnline) continue;
                totalOnline++;
                string friendName = friend.Name;
                if (!string.IsNullOrEmpty(filter) &&
                    friendName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (desired.Count >= MaxRows) continue; // still count totalOnline
                desired.Add(new KeyValuePair<ulong, string>(friend.Id.Value, friendName));
            }

            // Reuse pool rows in place, grow as needed, hide the surplus.
            int n = desired.Count;
            int existing = Mathf.Min(inviteRows.Count, n);
            for (int i = 0; i < existing; i++)
                inviteRows[i].Bind(desired[i].Key, desired[i].Value);
            for (int i = inviteRows.Count; i < n; i++)
            {
                InviteRowEntry row = BuildInviteRow();
                row.Bind(desired[i].Key, desired[i].Value);
                inviteRows.Add(row);
            }
            for (int i = n; i < inviteRows.Count; i++)
                inviteRows[i].go.SetActive(false);

            // Status: count + cap note.
            if (inviteStatusLabel != null)
            {
                if (n == 0)
                    inviteStatusLabel.text = string.IsNullOrEmpty(filter)
                        ? "No online friends found."
                        : "No online friends match that filter.";
                else if (totalOnline > MaxRows)
                    inviteStatusLabel.text = $"Showing {n} of {totalOnline} online friends — type to filter.";
                else
                    inviteStatusLabel.text = "";
            }

            // First-open diagnostic: how many of the desired rows already had a
            // cached avatar vs are still downloading. Rows fill via the
            // AvatarLateAssign poll once the download lands (no typing needed).
            if (n > 0)
            {
                int cached = 0;
                foreach (InviteRowEntry row in inviteRows)
                    if (row.avatar != null && row.avatar.texture != null) cached++;
                Debug.Log($"[ImasuSessionUI] Invite refresh: {n} rows, {cached} avatars cached, " +
                    $"{(n - cached)} still downloading (fill automatically).");
            }
        }

        void HideAllRows()
        {
            foreach (InviteRowEntry row in inviteRows)
                row.go.SetActive(false);
        }

        InviteRowEntry BuildInviteRow()
        {
            InviteRowEntry entry = new InviteRowEntry();

            GameObject row = new GameObject("FriendRow", typeof(RectTransform));
            row.transform.SetParent(inviteContent, false);
            entry.go = row;

            // Row background: subtle by default, brightens on hover so it's obvious
            // which friend you're about to invite. Single raycast target; children
            // never steal hover/clicks from it.
            Image rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(1f, 1f, 1f, 0.045f);
            rowImg.raycastTarget = true;
            entry.rowImg = rowImg;

            // Let drags starting on this row reach the owning ScrollRect.
            row.AddComponent<ScrollDragForwarder>();

            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 54;
            rowLe.preferredHeight = 54;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 7, 7);
            hlg.spacing = 12;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Square profile pic left of the name (appears once fetched). The
            // bottom-up UV flip is applied centrally by AvatarLateAssign.
            GameObject avGo = new GameObject("Avatar", typeof(RectTransform));
            avGo.transform.SetParent(row.transform, false);
            RawImage ri = avGo.AddComponent<RawImage>();
            ri.raycastTarget = false;
            entry.avatar = ri;
            entry.lateAssign = avGo.AddComponent<AvatarCache.AvatarLateAssign>();
            LayoutElement avLe = avGo.AddComponent<LayoutElement>();
            avLe.minWidth = 40;
            avLe.preferredWidth = 40;
            avLe.preferredHeight = 40;

            GameObject nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.fontSize = 17;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.raycastTarget = false;
            entry.nameText = nameTmp;
            LayoutElement nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;
            nameLe.minWidth = 80;

            // Invite button: hidden until the row is hovered so 100s of rows don't
            // each show a button. Pure visual — the row's Image is the single
            // raycast target (children never steal hover/clicks from it).
            GameObject btnGo = new GameObject("InviteButton", typeof(RectTransform));
            btnGo.transform.SetParent(row.transform, false);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.45f, 0.85f, 0f); // alpha 0 until hover
            btnImg.raycastTarget = false;
            entry.btnImg = btnImg;
            LayoutElement btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 92;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            TextMeshProUGUI txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = "Invite";
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            entry.btnText = txt;
            RectTransform trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Hover on the row reveals the button + brightens the row background;
            // a click anywhere on the row invites the CURRENT friend on this row
            // (entry.id updates as the pool reuses the row).
            UnityEngine.EventSystems.EventTrigger trigger = row.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                entry.rowImg.color = new Color(1f, 1f, 1f, 0.13f);
                entry.btnImg.color = new Color(0.2f, 0.45f, 0.85f, 1f);
            });

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                entry.rowImg.color = new Color(1f, 1f, 1f, 0.045f);
                entry.btnImg.color = new Color(0.2f, 0.45f, 0.85f, 0f);
            });

            var click = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            click.callback.AddListener(_ => InviteFriend(entry));

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
            trigger.triggers.Add(click);

            return entry;
        }

        void InviteFriend(InviteRowEntry entry)
        {
            global::Steamworks.SteamId id = (global::Steamworks.SteamId)entry.id;
            string friendName = entry.friendName;
            bool invited = false;
            try { invited = lobbyManager.currentLobby.InviteFriend(id); }
            catch (System.Exception e) { Debug.LogWarning($"[ImasuSessionUI] Invite failed for {friendName} ({id}): {e.Message}"); }

            Debug.Log($"[ImasuSessionUI] Invite {friendName} ({entry.id}): {(invited ? "sent" : "FAILED")}.");
            if (invited)
            {
                entry.btnText.text = "Invited";
                entry.btnImg.color = new Color(0.25f, 0.6f, 0.3f, 1f);
                if (inviteStatusLabel != null)
                    inviteStatusLabel.text = $"Invite sent to {friendName}.";
            }
            else
            {
                // Keep the row live so the user can retry; amber = failed.
                entry.btnText.text = "Retry";
                entry.btnImg.color = new Color(0.9f, 0.6f, 0.15f, 1f);
                if (inviteStatusLabel != null)
                    inviteStatusLabel.text = $"Could not invite {friendName} — lobby may be invite-only.";
            }
        }

        // ------------------------------------------------------------------ //
        //  uGUI factory helpers
        // ------------------------------------------------------------------ //

        GameObject Panel(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.11f, 0.12f, 0.14f, 0.96f);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        TextMeshProUGUI Label(Transform parent, string text, Vector2 anchor, TextAnchor align)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 16;
            t.color = Color.white;
            t.alignment = TMP_Align(align);
            t.raycastTarget = false;
            LayoutAt((RectTransform)go.transform, anchor);
            return t;
        }

        TextMeshProUGUI Label(Transform parent, string text, TextAnchor align)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 16;
            t.color = Color.white;
            t.alignment = TMP_Align(align);
            t.raycastTarget = false;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return t;
        }

        TextMeshProUGUI Label(Transform parent, string text, Vector2 anchor, TextAnchor align, float fontSize, Color color)
        {
            TextMeshProUGUI t = Label(parent, text, anchor, align);
            t.fontSize = fontSize;
            t.color = color;
            return t;
        }

        /// <summary>
        /// Clickable checkbox (uGUI Button toggling its own label). Returns the
        /// row GameObject so callers can position it via anchoredPosition.
        /// </summary>
        GameObject Checkbox(Transform parent, string label, Vector2 anchor, bool initial, System.Action<bool> onChanged)
        {
            bool state = initial;

            GameObject go = new GameObject("Checkbox_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;

            GameObject markGo = new GameObject("Check", typeof(RectTransform));
            markGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI mark = markGo.AddComponent<TextMeshProUGUI>();
            mark.text = "";
            mark.fontSize = 20;
            mark.fontStyle = FontStyles.Bold;
            mark.color = new Color(0.4f, 1f, 0.45f);
            mark.alignment = TextAlignmentOptions.Center;
            mark.raycastTarget = false;
            RectTransform mrt = (RectTransform)markGo.transform;
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(6, 0);
            mrt.offsetMax = new Vector2(-6, 0);

            GameObject txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 15;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Left;
            txt.raycastTarget = false;
            RectTransform trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(30, 0);
            trt.offsetMax = new Vector2(-4, 0);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(320, 30);

            void ApplyState()
            {
                mark.text = state ? "X" : "";
                bg.color = state ? new Color(0.15f, 0.4f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.06f);
            }

            ApplyState();
            button.onClick.AddListener(() =>
            {
                state = !state;
                ApplyState();
                onChanged?.Invoke(state);
            });

            return go;
        }

        void Title(Transform parent, string text)
        {
            GameObject go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 30;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 60);
            rt.anchoredPosition = new Vector2(0, -8);
        }

        bool Btn(Transform parent, string text, float anchorY, System.Action onClick)
        {
            GameObject go = new GameObject("Button_" + text.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.85f, 1);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;
            StyleButton(button);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            RectTransform trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, anchorY);
            rt.anchorMax = new Vector2(0.5f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 44);

            button.onClick.AddListener(() => onClick?.Invoke());
            return true;
        }

        /// <summary>Visible hover/press states for runtime buttons (the default
        /// Button colors leave every state white, so clicks look dead).</summary>
        static void StyleButton(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;                          // base Image.color shows through
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f);  // lighter on hover
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            cb.selectedColor = new Color(1.18f, 1.18f, 1.18f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            button.colors = cb;
        }

        bool Btn(Transform parent, string text, float anchorY, System.Action onClick, out GameObject created)
        {
            bool ok = Btn(parent, text, anchorY, onClick);
            created = parent.Find("Button_" + text.Replace(" ", ""))?.gameObject;
            return ok;
        }

        static TextAlignmentOptions TMP_Align(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                default: return TextAlignmentOptions.Left;
            }
        }

        TMP_InputField Input(Transform parent, string placeholder, Vector2 anchor, string initial = "")
        {
            GameObject go = new GameObject("Input", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1);

            TMP_InputField field = go.AddComponent<TMP_InputField>();
            LayoutAt((RectTransform)go.transform, anchor);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.color = Color.black;
            RectTransform trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 4);
            trt.offsetMax = new Vector2(-8, -4);

            field.textViewport = trt;
            field.textComponent = tmp;
            // FIX (R2): set field.text so the default value actually shows.
            field.text = initial;
            field.placeholder = null;

            if (initial == "")
            {
                GameObject ph = new GameObject("Placeholder", typeof(RectTransform));
                ph.transform.SetParent(go.transform, false);
                TextMeshProUGUI phTmp = ph.AddComponent<TextMeshProUGUI>();
                phTmp.text = placeholder;
                phTmp.fontSize = 16;
                phTmp.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                RectTransform prt = (RectTransform)ph.transform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = new Vector2(8, 4);
                prt.offsetMax = new Vector2(-8, -4);
                field.placeholder = phTmp;
            }

            return field;
        }

        /// <summary>
        /// Read-only, click-to-copy code display (not an editable text field).
        /// Clicking copies the code and briefly flashes "Copied!".
        /// </summary>
        TextMeshProUGUI CodeLabel(Transform parent, Vector2 anchor)
        {
            GameObject go = new GameObject("CodeLabel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.18f, 0.22f, 0.95f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.4f, 1f, 0.45f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            RectTransform trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = new Vector2(anchor.x, anchor.y);
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(360, 40);

            btn.onClick.AddListener(OnCopyLobbyCode);
            return tmp;
        }

        // ------------------------------------------------------------------ //
        //  Simple dropdown (shared implementation in SimpleDropdown.cs —
        //  TMP_Dropdown can't open without an authored template).
        // ------------------------------------------------------------------ //

        SimpleDropdown Dropdown(Transform parent, string[] options, Vector2 anchor, int initial = 0)
        {
            SimpleDropdown dd = SimpleDropdown.Create(parent, options, initial);
            LayoutAt((RectTransform)dd.transform, anchor);
            return dd;
        }

        GameObject BuildInviteScreen(Transform parent)
        {
            GameObject screen = Panel(parent, "InviteScreen");
            Title(screen.transform, "Invite Friends");

            Label(screen.transform, "Filter:", new Vector2(0.22f, 0.90f), TextAnchor.MiddleLeft);
            inviteSearch = Input(screen.transform, "Type a friend's name...", new Vector2(0.22f, 0.845f));
            inviteSearch.onValueChanged.AddListener(_ => RefreshInviteList());

            // Centered, narrower list than before — feels connected, not stretched.
            inviteContent = ScrollView(screen.transform, new Vector2(0.22f, 0.30f), new Vector2(0.78f, 0.80f));

            inviteStatusLabel = Label(screen.transform, "", new Vector2(0.22f, 0.24f), TextAnchor.MiddleLeft,
                14, new Color(1f, 1f, 1f, 0.75f));

            Btn(screen.transform, "Open Steam Overlay Invite", 0.15f, OnOverlayInvite);
            Btn(screen.transform, "Close", 0.06f, HideInvite);

            screen.SetActive(false);
            return screen;
        }

        RectTransform ScrollView(Transform parent, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject("ScrollView", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.35f);
            Layout((RectTransform)go.transform, min, max);

            ScrollRect scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(go.transform, false);
            viewport.AddComponent<Image>().color = Color.white; // Mask needs a graphic
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Layout((RectTransform)viewport.transform, Vector2.zero, Vector2.one);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform ct = (RectTransform)content.transform;
            ct.anchorMin = new Vector2(0, 1);
            ct.anchorMax = new Vector2(1, 1);
            ct.pivot = new Vector2(0.5f, 1);
            ct.sizeDelta = new Vector2(0, 0);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = ct;
            return ct;
        }

        void LayoutAt(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = new Vector2(anchor.x, anchor.y);
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(360, 40);
        }

        void Layout(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}