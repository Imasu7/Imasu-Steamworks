using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Self-building settings overlay with horizontal tabs (Game / Display /
    /// Audio / Controls). Deliberately lives OUTSIDE the session canvas so it
    /// can open from the main menu OR the in-match pause menu; DontDestroyOnLoad
    /// keeps it alive across scene changes once created.
    /// Escape closes it and stamps a frame flag that ImasuPauseMenu respects,
    /// so one keypress can't close settings AND toggle the pause menu.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        /// <summary>Frame in which Settings consumed the Escape key (-1 if none).</summary>
        public static int EscapeConsumedFrame => escapeConsumedFrame;

        static int escapeConsumedFrame = -1;

        Canvas canvas;
        GameObject panel;
        readonly List<GameObject> tabButtons = new List<GameObject>();
        readonly List<GameObject> tabPages = new List<GameObject>();
        int activeTab = -1;
        bool open;

        // live control refs for RefreshValues()
        SimpleDropdown dispModeDd, resDd, vsyncDd, qualityDd, voiceChatDd;
        Slider masterSlider, musicSlider, sfxSlider, voiceSlider;
        Slider sensSlider, voipSlider;
        TextMeshProUGUI masterVal, musicVal, sfxVal, voiceVal, sensVal, voipVal;

        string[] resolutionOptions = System.Array.Empty<string>();

        public static void Open()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("SettingsMenu");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<SettingsMenu>();
            }
            Instance.Show();
        }

        public static void Close()
        {
            if (Instance != null) Instance.Hide();
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
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Show()
        {
            if (panel == null) return;
            panel.SetActive(true);
            open = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SelectTab(activeTab < 0 ? 0 : activeTab);
            RefreshValues();
        }

        void Hide()
        {
            open = false;
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (!open) return;

            // Settings is a menu surface: always keep a usable cursor.
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                // A dropdown popup is open: close just that, not the whole panel.
                if (SimpleDropdown.HasOpenPopup)
                {
                    SimpleDropdown.ClosePopups();
                    return;
                }
                Hide();
                escapeConsumedFrame = Time.frameCount;
            }
        }

        // ------------------------------------------------------------------ //
        //  Construction
        // ------------------------------------------------------------------ //

        void BuildUi()
        {
            GameObject uiRoot = new GameObject("SettingsCanvas");
            uiRoot.transform.SetParent(transform, false);

            canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above session UI and pause menu
            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            uiRoot.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            panel = new GameObject("SettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(uiRoot.transform, false);
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0.07f, 0.08f, 0.10f, 0.94f);
            Stretch((RectTransform)panel.transform);

            Title(panel.transform, "Settings");

            // Horizontal tab bar.
            string[] tabs = { "Game", "Display", "Audio", "Controls" };
            float[] xs = { -330f, -110f, 110f, 330f };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                GameObject btn = TabButton(panel.transform, tabs[i], xs[i], () => SelectTab(index));
                tabButtons.Add(btn);
            }

            // Pages share a content rect; SelectTab toggles them.
            tabPages.Add(BuildGamePage(panel.transform));
            tabPages.Add(BuildDisplayPage(panel.transform));
            tabPages.Add(BuildAudioPage(panel.transform));
            tabPages.Add(BuildControlsPage(panel.transform));

            BtnAt(panel.transform, "Restore Defaults", new Vector2(0.5f, 0.055f), new Vector2(240, 42),
                () => { GameSettings.RestoreDefaults(); RefreshValues(); });
            BtnAt(panel.transform, "Back", new Vector2(0.5f, 0.055f), new Vector2(160, 42), Hide)
                .GetComponent<RectTransform>().anchoredPosition += new Vector2(220, 0);
            ((RectTransform)panel.transform.Find("Button_RestoreDefaults")).anchoredPosition += new Vector2(-140, 0);
        }

        GameObject BuildGamePage(Transform parent)
        {
            GameObject page = NewPage(parent, "GamePage");

            LabelAt(page.transform, "Voice Chat:", new Vector2(0.02f, 0.88f));
            voiceChatDd = MakeDropdown(page.transform, new Vector2(0.40f, 0.85f),
                new[] { "Off", "On" }, GameSettings.VoiceEnabled ? 1 : 0,
                i => GameSettings.VoiceEnabled = i == 1);

            TextMeshProUGUI note = LabelAt(page.transform,
                "Voice distance is set by the HOST in the Imasu Steamworks Control Center — " +
                "players only tune VOIP volume (Audio tab), which helps with loud/quiet microphones.",
                new Vector2(0.02f, 0.60f));
            note.color = new Color(1f, 1f, 1f, 0.45f);

            TextMeshProUGUI note2 = LabelAt(page.transform,
                "More gameplay options will land here as features are added.",
                new Vector2(0.02f, 0.30f));
            note2.color = new Color(1f, 1f, 1f, 0.45f);

            return page;
        }

        GameObject BuildDisplayPage(Transform parent)
        {
            GameObject page = NewPage(parent, "DisplayPage");

            LabelAt(page.transform, "Fullscreen Mode:", new Vector2(0.02f, 0.88f));
            dispModeDd = MakeDropdown(page.transform, new Vector2(0.40f, 0.85f),
                new[] { "Exclusive Fullscreen", "Borderless Window", "Windowed" }, GameSettings.DisplayMode,
                i => { GameSettings.DisplayMode = i; GameSettings.ApplyDisplay(); });

            LabelAt(page.transform, "Resolution:", new Vector2(0.02f, 0.70f));
            resolutionOptions = BuildResolutionOptions(out int currentResIndex);
            resDd = MakeDropdown(page.transform, new Vector2(0.40f, 0.67f),
                resolutionOptions, currentResIndex,
                i => ApplyResolution(i));

            LabelAt(page.transform, "VSync:", new Vector2(0.02f, 0.52f));
            vsyncDd = MakeDropdown(page.transform, new Vector2(0.40f, 0.49f),
                new[] { "Off", "On" }, GameSettings.Vsync ? 1 : 0,
                i => { GameSettings.Vsync = i == 1; GameSettings.ApplyDisplay(); });

            if (QualitySettings.names.Length > 0)
            {
                LabelAt(page.transform, "Quality Preset:", new Vector2(0.02f, 0.34f));
                qualityDd = MakeDropdown(page.transform, new Vector2(0.40f, 0.31f),
                    QualitySettings.names, GameSettings.QualityLevel,
                    i => { GameSettings.QualityLevel = i; GameSettings.ApplyDisplay(); });
            }
            else
            {
                LabelAt(page.transform, "Quality presets unavailable.", new Vector2(0.02f, 0.31f));
            }

            TextMeshProUGUI note = LabelAt(page.transform, "Changes apply immediately.", new Vector2(0.02f, 0.12f));
            note.color = new Color(1f, 1f, 1f, 0.45f);

            return page;
        }

        GameObject BuildAudioPage(Transform parent)
        {
            GameObject page = NewPage(parent, "AudioPage");

            masterVal = AddVolumeRow(page.transform, "Master", 0.88f, GameSettings.VolumeMaster,
                v => { GameSettings.VolumeMaster = v; GameSettings.ApplyAudio(); }, out masterSlider);
            musicVal = AddVolumeRow(page.transform, "Music", 0.72f, GameSettings.VolumeMusic,
                v => GameSettings.VolumeMusic = v, out musicSlider);
            sfxVal = AddVolumeRow(page.transform, "Sound Effects", 0.56f, GameSettings.VolumeSfx,
                v => GameSettings.VolumeSfx = v, out sfxSlider);
            voiceVal = AddVolumeRow(page.transform, "Voice Chat", 0.40f, GameSettings.VolumeVoice,
                v => GameSettings.VolumeVoice = v, out voiceSlider);
            voipVal = AddVoipRow(page.transform, "VOIP Volume (how loud others are)", 0.24f,
                GameSettings.VoiceUserVolume, v => GameSettings.VoiceUserVolume = v, out voipSlider);

            TextMeshProUGUI note = LabelAt(page.transform,
                "VOIP volume compensates for loud/quiet microphones. Voice distance is host-controlled in the Imasu Steamworks Control Center.",
                new Vector2(0.02f, 0.10f));
            note.fontSize = 13;
            note.color = new Color(1f, 1f, 1f, 0.45f);

            return page;
        }

        GameObject BuildControlsPage(Transform parent)
        {
            GameObject page = NewPage(parent, "ControlsPage");

            string[][] binds =
            {
                new[] { "Move", "W A S D" },
                new[] { "Jump", "Space" },
                new[] { "Pause / Menu", "Esc" },
                new[] { "Push To Talk", "V (toggle button in lobby)" }
            };

            float y = 0.90f;
            foreach (string[] bind in binds)
            {
                TextMeshProUGUI action = LabelAt(page.transform, bind[0], new Vector2(0.02f, y));
                action.fontStyle = FontStyles.Bold;
                LabelAt(page.transform, bind[1], new Vector2(0.35f, y));
                y -= 0.13f;
            }

            TextMeshProUGUI note = LabelAt(page.transform,
                "Key rebinding is planned — these are the current defaults.",
                new Vector2(0.02f, 0.08f));
            note.color = new Color(1f, 1f, 1f, 0.45f);

            sensVal = AddSensitivityRow(page.transform, "Mouse Sensitivity", 0.24f,
                GameSettings.MouseSens, v => GameSettings.MouseSens = v, out sensSlider);

            return page;
        }

        // ------------------------------------------------------------------ //
        //  Behaviour
        // ------------------------------------------------------------------ //

        void SelectTab(int index)
        {
            activeTab = index;
            for (int i = 0; i < tabPages.Count; i++)
                if (tabPages[i] != null)
                    tabPages[i].SetActive(i == index);

            for (int i = 0; i < tabButtons.Count; i++)
            {
                Image img = tabButtons[i]?.GetComponent<Image>();
                if (img != null)
                    img.color = i == index ? new Color(0.2f, 0.45f, 0.85f, 1) : new Color(0.24f, 0.26f, 0.30f, 1);
            }
        }

        void RefreshValues()
        {
            if (dispModeDd != null) dispModeDd.Set(GameSettings.DisplayMode);
            if (vsyncDd != null) vsyncDd.Set(GameSettings.Vsync ? 1 : 0);
            if (qualityDd != null) qualityDd.Set(Mathf.Clamp(GameSettings.QualityLevel, 0, qualityDd.options.Length - 1));
            if (voiceChatDd != null) voiceChatDd.Set(GameSettings.VoiceEnabled ? 1 : 0);

            if (resDd != null && resolutionOptions.Length > 0)
            {
                int idx = FindResolutionIndex(resolutionOptions, GameSettings.ResWidth, GameSettings.ResHeight);
                resDd.Set(idx);
            }

            if (masterSlider != null) { masterSlider.SetValueWithoutNotify(GameSettings.VolumeMaster); UpdatePct(masterVal, GameSettings.VolumeMaster); }
            if (musicSlider != null) { musicSlider.SetValueWithoutNotify(GameSettings.VolumeMusic); UpdatePct(musicVal, GameSettings.VolumeMusic); }
            if (sfxSlider != null) { sfxSlider.SetValueWithoutNotify(GameSettings.VolumeSfx); UpdatePct(sfxVal, GameSettings.VolumeSfx); }
            if (voiceSlider != null) { voiceSlider.SetValueWithoutNotify(GameSettings.VolumeVoice); UpdatePct(voiceVal, GameSettings.VolumeVoice); }
            if (sensSlider != null) { sensSlider.SetValueWithoutNotify(GameSettings.MouseSens); if (sensVal != null) sensVal.text = $"{GameSettings.MouseSens:0.0}x"; }
            if (voipSlider != null) { voipSlider.SetValueWithoutNotify(GameSettings.VoiceUserVolume); if (voipVal != null) voipVal.text = $"{GameSettings.VoiceUserVolume:0.0}x"; }
        }

        static void UpdatePct(TextMeshProUGUI label, float v)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(v * 100f)}%";
        }

        string[] BuildResolutionOptions(out int currentIndex)
        {
            List<Vector2Int> seen = new List<Vector2Int>();
            List<string> opts = new List<string>();

            foreach (Resolution r in Screen.resolutions)
            {
                Vector2Int wh = new Vector2Int(r.width, r.height);
                if (seen.Contains(wh)) continue;
                seen.Add(wh);
                opts.Add($"{r.width} x {r.height}");
            }

            // Biggest first (server-browser-ish ordering).
            for (int i = 0; i < seen.Count - 1; i++)
            {
                for (int j = i + 1; j < seen.Count; j++)
                {
                    long a = (long)seen[i].x * seen[i].y;
                    long b = (long)seen[j].x * seen[j].y;
                    if (b > a)
                    {
                        (seen[i], seen[j]) = (seen[j], seen[i]);
                        (opts[i], opts[j]) = (opts[j], opts[i]);
                    }
                }
            }

            currentIndex = FindResolutionIndex(opts.ToArray(), GameSettings.ResWidth, GameSettings.ResHeight);
            return opts.ToArray();
        }

        static int FindResolutionIndex(string[] options, int w, int h)
        {
            if (w <= 0 || h <= 0)
            {
                Resolution cur = Screen.currentResolution;
                w = cur.width;
                h = cur.height;
            }
            string want = $"{w} x {h}";
            for (int i = 0; i < options.Length; i++)
                if (options[i] == want)
                    return i;
            return 0;
        }

        void ApplyResolution(int index)
        {
            if (index < 0 || index >= resolutionOptions.Length) return;
            string[] parts = resolutionOptions[index].Split('x');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0].Trim(), out int w)) return;
            if (!int.TryParse(parts[1].Trim(), out int h)) return;

            GameSettings.ResWidth = w;
            GameSettings.ResHeight = h;
            GameSettings.ApplyDisplay();
        }

        // ------------------------------------------------------------------ //
        //  uGUI factory helpers (local copies matching ImasuSessionUI style)
        // ------------------------------------------------------------------ //

        void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            rt.sizeDelta = new Vector2(0, 50);
            rt.anchoredPosition = new Vector2(0, -10);
        }

        GameObject TabButton(Transform parent, string label, float xOffset, UnityAction onClick)
        {
            GameObject go = new GameObject("Tab_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.24f, 0.26f, 0.30f, 1);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 17;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            Stretch((RectTransform)textGo.transform);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(200, 42);
            rt.anchoredPosition = new Vector2(xOffset, -68);
            return go;
        }

        GameObject NewPage(Transform parent, string name)
        {
            GameObject page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);

            // Subtle backing so the content area reads as one block.
            Image bg = page.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.04f);
            bg.raycastTarget = false;

            RectTransform rt = (RectTransform)page.transform;
            rt.anchorMin = new Vector2(0.14f, 0.12f);
            rt.anchorMax = new Vector2(0.86f, 0.80f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return page;
        }

        TextMeshProUGUI LabelAt(Transform parent, string text, Vector2 anchor)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 16;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(anchor.x, anchor.y);
            rt.anchorMax = new Vector2(anchor.x, anchor.y);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(360, 30);
            return t;
        }

        GameObject BtnAt(Transform parent, string label, Vector2 anchor, Vector2 size, UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.85f, 1);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            Stretch((RectTransform)textGo.transform);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return go;
        }

        SimpleDropdown MakeDropdown(Transform parent, Vector2 anchor, string[] options, int initial, UnityAction<int> onChanged)
        {
            SimpleDropdown dd = SimpleDropdown.Create(parent, options, initial);

            RectTransform rt = (RectTransform)dd.transform;
            rt.anchorMin = new Vector2(anchor.x, anchor.y);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(360, 38);

            if (onChanged != null)
                dd.onValueChanged += v => onChanged(v);
            return dd;
        }

        TMP_InputField MakeInput(Transform parent, string placeholder, Vector2 anchor)
        {
            GameObject go = new GameObject("Input", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1);

            TMP_InputField field = go.AddComponent<TMP_InputField>();
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(anchor.x, anchor.y);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(360, 38);

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

            GameObject ph = new GameObject("Placeholder", typeof(RectTransform));
            ph.transform.SetParent(go.transform, false);
            TextMeshProUGUI phTmp = ph.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 16;
            phTmp.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
            phTmp.raycastTarget = false;
            RectTransform prt = (RectTransform)ph.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(8, 4);
            prt.offsetMax = new Vector2(-8, -4);
            field.placeholder = phTmp;

            return field;
        }

        TextMeshProUGUI AddVolumeRow(Transform parent, string labelText, float anchorY, float value,
            UnityAction<float> onChanged, out Slider slider)
        {
            LabelAt(parent, labelText, new Vector2(0.02f, anchorY));

            GameObject sliderGo = new GameObject($"Slider_{labelText.Replace(" ", "")}", typeof(RectTransform));
            sliderGo.transform.SetParent(parent, false);
            Image bg = sliderGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.15f);
            bg.raycastTarget = false;
            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(0.34f, anchorY);
            srt.anchorMax = new Vector2(0.34f, anchorY);
            srt.pivot = new Vector2(0, 0.5f);
            srt.sizeDelta = new Vector2(380, 26);

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = (RectTransform)fillArea.transform;
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = new Vector2(4, 4);
            fart.offsetMax = new Vector2(-4, -4);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.45f, 0.85f, 1);
            fillImg.raycastTarget = false;
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(value, 1);
            frt.sizeDelta = Vector2.zero;

            GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = (RectTransform)handleArea.transform;
            hart.anchorMin = Vector2.zero;
            hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(10, 0);
            hart.offsetMax = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = new Vector2(16, 0);
            hrt.anchorMin = new Vector2(value, 0);
            hrt.anchorMax = new Vector2(value, 1);

            slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));

            TextMeshProUGUI pct = LabelAt(parent, $"{Mathf.RoundToInt(value * 100f)}%", new Vector2(0.80f, anchorY));
            slider.onValueChanged.AddListener(v =>
            {
                onChanged?.Invoke(Mathf.Clamp01(v));
                UpdatePct(pct, v);
            });
            return pct;
        }

        /// <summary>Slider for the mouse-sensitivity multiplier (0.2–5, shown as "x").</summary>
        TextMeshProUGUI AddSensitivityRow(Transform parent, string labelText, float anchorY, float value,
            UnityAction<float> onChanged, out Slider slider)
        {
            const float min = 0.2f, max = 5f;
            LabelAt(parent, labelText, new Vector2(0.02f, anchorY));

            GameObject sliderGo = new GameObject($"Slider_{labelText.Replace(" ", "")}", typeof(RectTransform));
            sliderGo.transform.SetParent(parent, false);
            Image bg = sliderGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.15f);
            bg.raycastTarget = false;
            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(0.34f, anchorY);
            srt.anchorMax = new Vector2(0.34f, anchorY);
            srt.pivot = new Vector2(0, 0.5f);
            srt.sizeDelta = new Vector2(380, 26);

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = (RectTransform)fillArea.transform;
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = new Vector2(4, 4);
            fart.offsetMax = new Vector2(-4, -4);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.45f, 0.85f, 1);
            fillImg.raycastTarget = false;
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(Mathf.InverseLerp(min, max, value), 1);
            frt.sizeDelta = Vector2.zero;

            GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = (RectTransform)handleArea.transform;
            hart.anchorMin = Vector2.zero;
            hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(10, 0);
            hart.offsetMax = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = new Vector2(16, 0);
            hrt.anchorMin = new Vector2(Mathf.InverseLerp(min, max, value), 0);
            hrt.anchorMax = new Vector2(Mathf.InverseLerp(min, max, value), 1);

            slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));

            TextMeshProUGUI val = LabelAt(parent, $"{value:0.0}x", new Vector2(0.80f, anchorY));
            slider.onValueChanged.AddListener(v =>
            {
                float clamped = Mathf.Clamp(v, min, max);
                onChanged?.Invoke(clamped);
                val.text = $"{clamped:0.0}x";
            });
            return val;
        }

        /// <summary>Per-user VOIP gain slider (0..2, shown as "x1.0").</summary>
        TextMeshProUGUI AddVoipRow(Transform parent, string labelText, float anchorY, float value,
            UnityAction<float> onChanged, out Slider slider)
        {
            const float min = 0f, max = 2f;
            LabelAt(parent, labelText, new Vector2(0.02f, anchorY));

            GameObject sliderGo = new GameObject($"Slider_{labelText.Replace(" ", "")}", typeof(RectTransform));
            sliderGo.transform.SetParent(parent, false);
            Image bg = sliderGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.15f);
            bg.raycastTarget = false;
            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(0.34f, anchorY);
            srt.anchorMax = new Vector2(0.34f, anchorY);
            srt.pivot = new Vector2(0, 0.5f);
            srt.sizeDelta = new Vector2(380, 26);

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = (RectTransform)fillArea.transform;
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = new Vector2(4, 4);
            fart.offsetMax = new Vector2(-4, -4);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.45f, 0.85f, 1);
            fillImg.raycastTarget = false;
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(Mathf.InverseLerp(min, max, value), 1);
            frt.sizeDelta = Vector2.zero;

            GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = (RectTransform)handleArea.transform;
            hart.anchorMin = Vector2.zero;
            hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(10, 0);
            hart.offsetMax = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = new Vector2(16, 0);
            hrt.anchorMin = new Vector2(Mathf.InverseLerp(min, max, value), 0);
            hrt.anchorMax = new Vector2(Mathf.InverseLerp(min, max, value), 1);

            slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));

            TextMeshProUGUI val = LabelAt(parent, $"{value:0.0}x", new Vector2(0.80f, anchorY));
            slider.onValueChanged.AddListener(v =>
            {
                float clamped = Mathf.Clamp(v, min, max);
                onChanged?.Invoke(clamped);
                val.text = $"{clamped:0.0}x";
            });
            return val;
        }
    }
}
