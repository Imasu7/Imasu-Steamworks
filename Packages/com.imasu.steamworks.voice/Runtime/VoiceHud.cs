using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Always-on compact HUD showing who is talking. Green = a remote player's
    /// voice is arriving, cyan = your own mic is live. Scene-only component:
    /// drop it in the match scene (ProductionSceneBuilder does this).
    /// Builds its own uGUI so there is nothing to wire up by hand.
    /// </summary>
    public class VoiceHud : MonoBehaviour
    {
        Canvas canvas;
        RectTransform panelRect;
        readonly List<TextMeshProUGUI> rowLabels = new List<TextMeshProUGUI>();
        readonly List<VoicePlayer> players = new List<VoicePlayer>();

        float nextRefresh;

        const float RefreshInterval = 0.25f;
        static readonly Color SpeakingColor = new Color(0.4f, 1f, 0.45f);
        static readonly Color MicColor = new Color(0.35f, 0.9f, 1f);
        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.55f);

        void Start()
        {
            BuildUi();
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        void BuildUi()
        {
            GameObject root = new GameObject("VoiceHudRoot");
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(1, 1);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.pivot = new Vector2(1, 1);
            panelRect.anchoredPosition = new Vector2(-12, -12);
            panelRect.sizeDelta = new Vector2(280, 0);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperRight;

            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = NewRow();
            title.text = "Voice";
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Right;
        }

        TextMeshProUGUI NewRow()
        {
            GameObject go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(panelRect, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 15;
            tmp.color = IdleColor;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.raycastTarget = false; // clicks pass through the HUD
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 20;
            le.preferredHeight = 20;
            rowLabels.Add(tmp);
            return tmp;
        }

        void Refresh()
        {
            if (canvas == null || panelRect == null) return;

            players.Clear();
            foreach (VoicePlayer vp in FindObjectsByType<VoicePlayer>(FindObjectsSortMode.None))
                players.Add(vp);
            // Local player first.
            players.Sort((a, b) => (a.isLocalPlayer ? 0 : 1).CompareTo(b.isLocalPlayer ? 0 : 1));

            if (players.Count == 0)
            {
                canvas.enabled = false;
                return;
            }
            canvas.enabled = true;

            // Keep one extra label for the title (index 0).
            while (rowLabels.Count > players.Count + 1)
            {
                Destroy(rowLabels[rowLabels.Count - 1].gameObject);
                rowLabels.RemoveAt(rowLabels.Count - 1);
            }
            while (rowLabels.Count < players.Count + 1)
                NewRow();

            for (int i = 0; i < players.Count; i++)
            {
                VoicePlayer vp = players[i];
                TextMeshProUGUI lbl = rowLabels[i + 1];
                bool mine = vp.isLocalPlayer;
                bool active = mine ? vp.isMicLive : vp.isSpeaking;

                string suffix = mine
                    ? (vp.isMicLive ? "  [mic live]" : "")
                    : (vp.isSpeaking ? "  [talking]" : "");
                lbl.text = $"{vp.GetDisplayName()}{suffix}";
                lbl.color = mine
                    ? (vp.isMicLive ? MicColor : IdleColor)
                    : (vp.isSpeaking ? SpeakingColor : IdleColor);
            }
        }
    }
}
