using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Minimal selectable value box for runtime-built UI. TMP_Dropdown cannot
    /// open without an authored template (which procedurally-built UI doesn't
    /// have), so this pairs a caption label with an on-demand popup option
    /// list. Shared by ImasuSessionUI and SettingsMenu.
    /// </summary>
    public class SimpleDropdown : MonoBehaviour
    {
        public string[] options = System.Array.Empty<string>();
        public int value;

        /// <summary>Raised when Set() changes (or confirms) the selected index.</summary>
        public event System.Action<int> onValueChanged;

        /// <summary>Live popup blockers (invisible full-screen click-catchers).</summary>
        static readonly System.Collections.Generic.HashSet<GameObject> openBlockers =
            new System.Collections.Generic.HashSet<GameObject>();

        /// <summary>True while any dropdown popup is open.</summary>
        public static bool HasOpenPopup => openBlockers.Count > 0;

        /// <summary>ESC handler: closes any open dropdown popups.</summary>
        public static void ClosePopups()
        {
            if (openBlockers.Count == 0) return;
            foreach (GameObject b in new System.Collections.Generic.List<GameObject>(openBlockers))
                if (b != null) Object.Destroy(b);
            openBlockers.Clear();
        }

        TextMeshProUGUI caption;

        /// <summary>Creates the value box + caption under <paramref name="parent"/>. Position via its RectTransform.</summary>
        public static SimpleDropdown Create(Transform parent, string[] options, int initial)
        {
            GameObject go = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.85f, 0.85f, 1);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            SimpleDropdown dd = go.AddComponent<SimpleDropdown>();
            dd.options = options ?? System.Array.Empty<string>();

            GameObject capGo = new GameObject("Caption", typeof(RectTransform));
            capGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI capTmp = capGo.AddComponent<TextMeshProUGUI>();
            capTmp.fontSize = 16;
            capTmp.color = Color.black;
            capTmp.alignment = TextAlignmentOptions.Left;
            capTmp.raycastTarget = false;
            RectTransform capRt = (RectTransform)capGo.transform;
            capRt.anchorMin = Vector2.zero;
            capRt.anchorMax = Vector2.one;
            capRt.offsetMin = new Vector2(10, 0);
            capRt.offsetMax = new Vector2(-10, 0);
            dd.caption = capTmp;

            btn.onClick.AddListener(() => OpenPopup(dd, (RectTransform)go.transform));

            dd.Set(initial);
            return dd;
        }

        public void Set(int index)
        {
            if (options.Length == 0) return;
            int clamped = Mathf.Clamp(index, 0, options.Length - 1);
            bool changed = clamped != value;
            value = clamped;
            if (caption != null) caption.text = options[value];
            if (changed) onValueChanged?.Invoke(value);
        }

        /// <summary>Opens the option list just below the value box; clicking elsewhere closes it.</summary>
        public static void OpenPopup(SimpleDropdown dd, RectTransform owner)
        {
            if (dd.options.Length == 0) return;

            // Popup lives on the owning screen so it dies when the screen hides.
            Transform root = owner;
            while (root.parent != null && root.parent.GetComponent<Canvas>() == null)
                root = root.parent;

            // Invisible full-screen blocker: clicking anywhere else closes.
            GameObject blocker = new GameObject("DropdownBlocker", typeof(RectTransform));
            blocker.transform.SetParent(root, false);
            Image blockerImg = blocker.AddComponent<Image>();
            blockerImg.color = new Color(0, 0, 0, 0); // invisible but clickable
            Button blockerBtn = blocker.AddComponent<Button>();
            blockerBtn.targetGraphic = blockerImg;
            RectTransform brt = (RectTransform)blocker.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            blockerBtn.onClick.AddListener(() => { UnregisterBlocker(blocker); Destroy(blocker); });
            openBlockers.Add(blocker);

            const float rowHeight = 38f;
            GameObject panel = new GameObject("DropdownOptions", typeof(RectTransform));
            panel.transform.SetParent(blocker.transform, false);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 3;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(owner.anchorMin.x, owner.anchorMin.y - (rowHeight + 6f) / 720f);
            prt.anchorMax = prt.anchorMin;
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(360f, 0f);

            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.13f, 0.14f, 0.17f, 0.98f);
            panelImg.raycastTarget = true; // clicks inside don't fall through to the blocker

            for (int i = 0; i < dd.options.Length; i++)
            {
                int index = i;
                string option = dd.options[i];

                GameObject opt = new GameObject("Option_" + option.Replace(" ", ""), typeof(RectTransform));
                opt.transform.SetParent(panel.transform, false);
                Image optImg = opt.AddComponent<Image>();
                optImg.color = i == dd.value
                    ? new Color(0.2f, 0.45f, 0.85f, 1)
                    : new Color(1f, 1f, 1f, 0.08f);
                Button optBtn = opt.AddComponent<Button>();
                optBtn.targetGraphic = optImg;
                LayoutElement le = opt.AddComponent<LayoutElement>();
                le.minHeight = rowHeight - 4;
                le.preferredHeight = rowHeight - 4;

                GameObject txtGo = new GameObject("Text", typeof(RectTransform));
                txtGo.transform.SetParent(opt.transform, false);
                TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
                tmp.text = option;
                tmp.fontSize = 15;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.raycastTarget = false;
                RectTransform trt = (RectTransform)txtGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10, 2);
                trt.offsetMax = new Vector2(-8, -2);

                optBtn.onClick.AddListener(() =>
                {
                    dd.Set(index);
                    UnregisterBlocker(blocker);
                    Destroy(blocker);
                });
            }
        }

        static void UnregisterBlocker(GameObject blocker)
        {
            openBlockers.Remove(blocker);
        }
    }
}
