using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Hold-Tab scoreboard overlay. Lists connected players with their Steam
    /// avatar inside a status ring, name and ping (ms).
    ///
    /// Ring colour:
    ///   green  - talking (own mic live, or this player's voice arriving)
    ///   red    - mic confirmed broken (capture error)  [confirmed-failure only]
    ///   grey   - no mic verdict yet / no mic in play
    ///
    /// In the waiting room there are no player objects yet, so it falls back to
    /// the Steam lobby member list (ping shows "—").
    /// Passive overlay: never grabs the cursor and never blocks UI raycasts.
    /// </summary>
    public class ScoreboardUI : MonoBehaviour
    {
        public static ScoreboardUI Instance { get; private set; }

        const float RefreshInterval = 0.2f;
        static readonly Color Talking = new Color(0.4f, 1f, 0.45f);
        static readonly Color MicFailed = new Color(1f, 0.32f, 0.3f);
        static readonly Color Neutral = new Color(0.34f, 0.37f, 0.42f);

        GameObject panel;
        RectTransform content;
        float nextRefresh;

        public static void Ensure()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("ScoreboardUI");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ScoreboardUI>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
            panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            bool want = NetworkClient.active &&
                        Keyboard.current != null && Keyboard.current.tabKey.isPressed;

            if (panel.activeSelf != want)
            {
                panel.SetActive(want);
                nextRefresh = 0f;
            }

            if (!want || Time.time < nextRefresh) return;
            nextRefresh = Time.time + RefreshInterval;
            Refresh();
        }

        // ------------------------------------------------------------------ //
        //  Data
        // ------------------------------------------------------------------ //

        void Refresh()
        {
            for (int i = 0; i < content.childCount; i++)
            {
                GameObject row = content.GetChild(i).gameObject;
                row.SetActive(false);
                Destroy(row);
            }

            var players = new List<IVoiceMember>();
            foreach (NetworkIdentity ni in NetworkClient.spawned.Values)
            {
                IVoiceMember vp = ni.GetComponent<IVoiceMember>();
                if (vp != null) players.Add(vp);
            }

            if (players.Count > 0)
            {
                players.Sort((a, b) =>
                {
                    bool la = a.IsLocalPlayer, lb = b.IsLocalPlayer;
                    if (la != lb) return la ? -1 : 1;
                    return string.Compare(a.DisplayName, b.DisplayName,
                        System.StringComparison.OrdinalIgnoreCase);
                });

                foreach (IVoiceMember vp in players) AddPlayerRow(vp);
                return;
            }

            // No player objects (waiting room): fall back to the lobby member list.
            SteamLobbyManager lobby = SteamLobbyManager.instance;
            if (lobby == null || lobby.members.Count == 0)
            {
                AddInfoRow("No players");
                return;
            }

            var members = new List<KeyValuePair<global::Steamworks.SteamId, string>>(lobby.members);
            members.Sort((x, y) => string.Compare(x.Value, y.Value,
                System.StringComparison.OrdinalIgnoreCase));
            foreach (var kv in members)
                AddLobbyRow(kv.Value, kv.Key.Value);
        }

        // ------------------------------------------------------------------ //
        //  Rows
        // ------------------------------------------------------------------ //

void AddPlayerRow(IVoiceMember vp)
        {
            PlayerRtt rtt = (vp as Component)?.GetComponent<PlayerRtt>();

            Color ring = Neutral;
            if (vp.MicFailed) ring = MicFailed;
            else if (vp.IsLocalPlayer ? vp.MicLive : vp.Speaking) ring = Talking;

            string ping = rtt != null ? $"{rtt.rttMs:0} ms" : "—";
            AddRow(vp.DisplayName, vp.OwnerSteamId, ring, ping);
        }

        void AddLobbyRow(string name, ulong steamId)
        {
            AddRow(name, steamId, Neutral, "—");
        }

        void AddRow(string name, ulong steamId, Color ringColor, string ping)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(content, false);

            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 34;
            rowLe.preferredHeight = 34;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(6, 6, 2, 2);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Ring (status colour) with the avatar centred inside it.
            GameObject ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(row.transform, false);
            Image ringImg = ringGo.AddComponent<Image>();
            ringImg.color = ringColor;
            ringImg.raycastTarget = false;
            LayoutElement ringLe = ringGo.AddComponent<LayoutElement>();
            ringLe.preferredWidth = 32;
            ringLe.preferredHeight = 32;
            ((RectTransform)ringGo.transform).sizeDelta = new Vector2(32, 32);

            GameObject avGo = new GameObject("Avatar", typeof(RectTransform));
            avGo.transform.SetParent(ringGo.transform, false);
            RawImage ri = avGo.AddComponent<RawImage>();
            ri.raycastTarget = false;
            RectTransform avRt = (RectTransform)avGo.transform;
            avRt.anchorMin = new Vector2(0.5f, 0.5f);
            avRt.anchorMax = new Vector2(0.5f, 0.5f);
            avRt.sizeDelta = new Vector2(26, 26);
            if (steamId != 0)
                avGo.AddComponent<AvatarCache.AvatarLateAssign>().Init(steamId, ri);

            // Name.
            GameObject nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = name;
            nameTmp.fontSize = 15;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.raycastTarget = false;
            LayoutElement nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;
            nameLe.minWidth = 80;

            // Ping.
            GameObject pingGo = new GameObject("Ping", typeof(RectTransform));
            pingGo.transform.SetParent(row.transform, false);
            TextMeshProUGUI pingTmp = pingGo.AddComponent<TextMeshProUGUI>();
            pingTmp.text = ping;
            pingTmp.fontSize = 14;
            pingTmp.color = new Color(0.9f, 0.93f, 1f);
            pingTmp.alignment = TextAlignmentOptions.Right;
            pingTmp.raycastTarget = false;
            LayoutElement pingLe = pingGo.AddComponent<LayoutElement>();
            pingLe.preferredWidth = 64;
        }

        void AddInfoRow(string text)
        {
            GameObject row = new GameObject("InfoRow", typeof(RectTransform));
            row.transform.SetParent(content, false);

            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 34;
            rowLe.preferredHeight = 34;

            TextMeshProUGUI tmp = row.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 15;
            tmp.color = new Color(0.7f, 0.73f, 0.8f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        // ------------------------------------------------------------------ //
        //  Construction
        // ------------------------------------------------------------------ //

        void BuildUi()
        {
            GameObject canvasGo = new GameObject("ScoreboardCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450; // below SettingsMenu (500)
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // No raycasts through the scoreboard (it must never eat clicks).
            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // Backdrop: soft dark sheet, right-aligned.
            GameObject bgGo = new GameObject("Panel", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            Image bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;
            RectTransform bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(1f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.pivot = new Vector2(1f, 1f);
            bgRt.anchoredPosition = new Vector2(-20, -20);
            bgRt.sizeDelta = new Vector2(420, -40);

            // Column header.
            GameObject headGo = new GameObject("Header", typeof(RectTransform));
            headGo.transform.SetParent(bgRt, false);
            HorizontalLayoutGroup headHlg = headGo.AddComponent<HorizontalLayoutGroup>();
            headHlg.padding = new RectOffset(42, 6, 2, 2);
            headHlg.spacing = 8;
            headHlg.childControlWidth = true;
            headHlg.childForceExpandWidth = false;
            headHlg.childAlignment = TextAnchor.UpperLeft;

            TextMeshProUGUI nameHead = MakeHeaderCell("Player");
            nameHead.transform.SetParent(headGo.transform, false);
            LayoutElement nameHeadLe = nameHead.gameObject.AddComponent<LayoutElement>();
            nameHeadLe.flexibleWidth = 1;
            nameHeadLe.minWidth = 80;

            TextMeshProUGUI pingHead = MakeHeaderCell("Ping");
            pingHead.alignment = TextAlignmentOptions.Right;
            pingHead.transform.SetParent(headGo.transform, false);
            LayoutElement pingHeadLe = pingHead.gameObject.AddComponent<LayoutElement>();
            pingHeadLe.preferredWidth = 64;

            RectTransform headRt = (RectTransform)headGo.transform;
            headRt.anchorMin = new Vector2(0, 1f);
            headRt.anchorMax = new Vector2(1f, 1f);
            headRt.pivot = new Vector2(0.5f, 1f);
            headRt.sizeDelta = new Vector2(0, 28);
            headRt.anchoredPosition = new Vector2(0, 0);

            // Rows scroll area.
            GameObject rowsGo = new GameObject("Rows", typeof(RectTransform));
            rowsGo.transform.SetParent(bgRt, false);
            RectTransform rowsRt = (RectTransform)rowsGo.transform;
            rowsRt.anchorMin = new Vector2(0, 0);
            rowsRt.anchorMax = new Vector2(1f, 1f);
            rowsRt.offsetMin = new Vector2(0, 0);
            rowsRt.offsetMax = new Vector2(0, -28);

            VerticalLayoutGroup vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            content = rowsRt;
            panel = bgGo;
        }

        static TextMeshProUGUI MakeHeaderCell(string text)
        {
            GameObject go = new GameObject("HeaderCell", typeof(RectTransform));
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.75f, 0.8f, 0.9f);
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}