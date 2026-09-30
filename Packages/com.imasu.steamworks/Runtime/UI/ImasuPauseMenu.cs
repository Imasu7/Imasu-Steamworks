using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
        /// <summary>
        /// In-match pause menu (R2). Drop this component into the game/match scene.
        /// Press Escape to toggle. It is an OVERLAY ONLY — the simulation keeps
        /// running underneath (multiplayer listen-server; no Time.timeScale).
        /// Buttons:
        ///   Resume          - close the menu
        ///   Settings        - open the shared settings overlay on top of the menu
        ///   Back to Lobby   - HOST ONLY: loads the waiting room for everyone,
        ///                     so the party keeps the lobby between matches
        ///   Quit to Menu    - stop the session and let Mirror load offlineScene
        /// Quitting the application lives only on the main menu.
        /// Builds its own uGUI at runtime, like ImasuSessionUI.
        /// </summary>
    public class ImasuPauseMenu : MonoBehaviour
    {
        public static ImasuPauseMenu Instance { get; private set; }

        /// <summary>True while the pause overlay is open (drives controller cursor gates).</summary>
        public static bool IsOpen => Instance != null && Instance.open;

        ImasuNetworkManager networkManager;
        Canvas canvas;
        GameObject panel;
        GameObject backToLobbyButton;
        GameObject changeMapButton;
        bool open;
        bool wasCursorVisible;
        CursorLockMode wasCursorLock;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            networkManager = FindFirstObjectByType<ImasuNetworkManager>();
            BuildUi();
            SetOpen(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
                return;

            // If settings just consumed Escape this frame, don't also toggle us.
            if (Time.frameCount == SettingsMenu.EscapeConsumedFrame)
                return;

            SetOpen(!open);
        }

        void OnDisable()
        {
            // Scene change / destroy while the menu is open must never leave
            // the cursor hidden.
            if (open)
            {
                open = false;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        void BuildUi()
        {
            GameObject uiRoot = new GameObject("PauseCanvas");
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

            panel = new GameObject("PausePanel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
            RectTransform prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            Title(panel.transform, "Paused");
            Btn(panel.transform, "Resume", 0.62f, () => SetOpen(false));
            Btn(panel.transform, "Settings", 0.51f, () => SettingsMenu.Open());

            backToLobbyButton = CreateButton(panel.transform, "Back to Lobby", 0.40f, OnBackToLobby);
            // Party flow: only the host can pull everyone back to the waiting room.
            backToLobbyButton.SetActive(NetworkServer.active);

            changeMapButton = CreateButton(panel.transform, "Change Map", 0.29f, OnChangeMap);
            changeMapButton.SetActive(NetworkServer.active);

            Btn(panel.transform, "Quit to Menu", 0.18f, OnQuitToMenu);
        }

        // ------------------------------------------------------------------ //
        //  Actions
        // ------------------------------------------------------------------ //

        void OnBackToLobby()
        {
            if (networkManager == null) networkManager = FindFirstObjectByType<ImasuNetworkManager>();
            if (networkManager == null) return;

            if (NetworkServer.active)
            {
                // Host: send everyone back to the waiting room.
                if (!string.IsNullOrEmpty(networkManager.onlineScene))
                    networkManager.ServerChangeScene(networkManager.onlineScene);
            }
            else if (NetworkClient.active)
            {
                // Client: just disconnect; the host keeps the lobby going.
                networkManager.StopClient();
            }

            SetOpen(false);
        }

        void OnQuitToMenu()
        {
            if (networkManager == null) networkManager = FindFirstObjectByType<ImasuNetworkManager>();
            networkManager?.StopLocalNetwork();
            SetOpen(false);
            // Mirror loads offlineScene automatically when the session stops.
        }

        // ------------------------------------------------------------------ //
        //  Change map (host only)
        // ------------------------------------------------------------------ //

        /// <summary>Flattens the config-driven catalog into (label, scene) pairs for the switch popup.</summary>
        void BuildMapChoices(out string[] names, out string[] scenes)
        {
            var n = new List<string>();
            var s = new List<string>();

            if (networkManager != null)
            {
                if (networkManager.gameModes != null)
                    foreach (ImasuNetworkManager.GameModeEntry mode in networkManager.gameModes)
                        if (mode.maps != null)
                            foreach (ImasuNetworkManager.MapEntry m in mode.maps)
                            {
                                if (string.IsNullOrEmpty(m.sceneName)) continue;
                                string modeTag = string.IsNullOrEmpty(mode.displayName) ? "" : mode.displayName + " — ";
                                n.Add(modeTag + (string.IsNullOrEmpty(m.displayName) ? m.sceneName : m.displayName));
                                s.Add(m.sceneName);
                            }
                if (networkManager.maps != null)
                    foreach (ImasuNetworkManager.MapEntry m in networkManager.maps)
                    {
                        if (string.IsNullOrEmpty(m.sceneName)) continue;
                        n.Add(string.IsNullOrEmpty(m.displayName) ? m.sceneName : m.displayName);
                        s.Add(m.sceneName);
                    }
                if (n.Count == 0 && !string.IsNullOrEmpty(networkManager.matchScene))
                {
                    n.Add(networkManager.matchScene);
                    s.Add(networkManager.matchScene);
                }
            }

            names = n.ToArray();
            scenes = s.ToArray();
        }

        void OnChangeMap()
        {
            if (networkManager == null) networkManager = FindFirstObjectByType<ImasuNetworkManager>();
            if (networkManager == null) return;

            BuildMapChoices(out string[] names, out string[] scenes);
            if (names.Length == 0) return;

            SimpleDropdown dd = SimpleDropdown.Create(panel.transform, names, 0);
            SimpleDropdown.OpenPopup(dd, (RectTransform)dd.transform);

            dd.onValueChanged += idx =>
            {
                if (idx < 0 || idx >= scenes.Length) return;
                string scene = scenes[idx];

                // Prefer the lobby-aware path (updates the browser's advertised map).
                var lobby = SteamLobbyManager.instance;
                if (lobby != null && lobby.inLobby && NetworkServer.active)
                    lobby.ServerChangeMap(scene);
                else
                    networkManager.ServerChangeMap(scene);

                SetOpen(false);
            };
        }

        // ------------------------------------------------------------------ //
        //  Open / close
        // ------------------------------------------------------------------ //

        void SetOpen(bool value)
        {
            open = value;
            if (panel != null) panel.SetActive(value);

            // Deliberately no Time.timeScale change: this is a multiplayer
            // listen-server, freezing the host freezes everyone's sim and
            // desyncs clients. The pause menu is an overlay only.
            if (value)
            {
                wasCursorVisible = Cursor.visible;
                wasCursorLock = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = wasCursorVisible;
                Cursor.lockState = wasCursorLock;
            }
        }

        // ------------------------------------------------------------------ //
        //  uGUI helpers
        // ------------------------------------------------------------------ //

        void Title(Transform parent, string text)
        {
            GameObject go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 36;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 60);
            rt.anchoredPosition = new Vector2(0, -40);
        }

        void Btn(Transform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
            => CreateButton(parent, label, anchorY, onClick);

        GameObject CreateButton(Transform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.85f, 1);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
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
            rt.sizeDelta = new Vector2(320, 48);

            if (onClick != null)
                button.onClick.AddListener(onClick);
            return go;
        }
    }
}