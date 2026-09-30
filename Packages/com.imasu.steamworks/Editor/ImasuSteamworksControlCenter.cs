using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Imasu.Steamworks.Editor
{
    /// <summary>
    /// Imasu Steamworks Control Center — the master hub for the plugin.
    /// One Active Config drives everything; tabs group the settings:
    ///
    ///   Home                — config picker, kill-switch, getting-started checklist, quick actions
    ///   Project & Session   — scenes, session defaults, Steam (global)
    ///   Gamemodes & Maps    — the gamemode/map catalog + per-mode overrides (canonical editor)
    ///   Spawns & Teams      — scene-aware spawn add + team config
    ///   Advanced            — every remaining config field (grouped; gamemodes/maps live above)
    ///
    /// Packages are managed from the separate Imasu → Package Manager window.
    /// </summary>
    public class ImasuSteamworksControlCenter : EditorWindow
    {
        static ImasuSteamworksControlCenter ControlCenterInstance;

        const string ConfigFolder = "Assets/Config";
        const string ConfigPath = ConfigFolder + "/ImasuSteamworksConfig.asset";
        const string ActiveConfigPref = "ImasuSteamworks.ActiveConfigPath";

        const string SessionPrefabPath = "Packages/com.imasu.steamworks/Prefabs/ImasuSessionManager.prefab";

        ImasuSteamworksConfig config;
        UnityEditor.Editor configEditor;
        Vector2 scroll;
        int tab;
        string[] tabNames =
        {
            "Home", "Project & Session", "Gamemodes & Maps", "Spawns & Teams", "Advanced"
        };
        const int TabHome = 0;
        const int TabProject = 1;
        const int TabGamemodes = 2;
        const int TabSpawns = 3;
        const int TabAdvanced = 4;

        // Getting-started checklist state (folded into Home).
        bool wizardFoldout = true;
        readonly bool[] wizardDone = new bool[5];

        // Gamemode / map editor state.
        readonly Dictionary<int, bool> gamemodeFoldouts = new Dictionary<int, bool>();
        readonly Dictionary<int, bool> overrideFoldouts = new Dictionary<int, bool>();
        readonly Dictionary<string, ReorderableList> mapLists =
            new Dictionary<string, ReorderableList>();   // keyed by maps property path
        SerializedObject teamSO;
        double lastAutoApply = double.MinValue;

        // Perf caches — this window repaints on every mouse move, so nothing here
        // may scan the project, the scene, or the package registry per repaint (D24).
        string spawnScanScene;
        double spawnScanAt = double.MinValue;
        SpawnPoint[] spawnPoints = new SpawnPoint[0];
        SpawnArea[] spawnAreas = new SpawnArea[0];
        bool hasMaster;
        bool hasMasterValid;
        string lastScenePath;

        readonly Dictionary<string, SceneValidation> sceneValidation =
            new Dictionary<string, SceneValidation>(StringComparer.OrdinalIgnoreCase);
        bool validationStale = true;

        struct SceneValidation
        {
            public bool hasMaster;
            public int spawnCount;
            public bool Failed => !hasMaster || spawnCount == 0;
        }

        [MenuItem("Imasu/Steamworks Control Center")]
        public static void Open()
        {
            var win = GetWindow<ImasuSteamworksControlCenter>("Imasu Steamworks Control Center");
            win.minSize = new Vector2(480, 620);
            win.Show();
        }

        void OnEnable()
        {
            ControlCenterInstance = this;
            SceneCache.Invalidate();
            LoadConfig();
            EditorApplication.projectChanged += OnProjectChanged;

            // Auto-apply the Active Config to the Session Managers in the open scenes so
            // nothing reports "misconfigured" after a domain reload (previously you had
            // to unselect + reselect the config to force an apply).
            if (config != null)
                AutoApplyToSceneMasters();
        }

        void OnDisable()
        {
            if (ControlCenterInstance == this) ControlCenterInstance = null;
            if (configEditor != null) DestroyImmediate(configEditor);
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        void OnProjectChanged()
        {
            SceneCache.Invalidate();
            spawnScanAt = double.MinValue;
            hasMasterValid = false;
            InvalidateValidation();
        }

        void LoadConfig()
        {
            if (config != null) return;

            // 1. The persisted Active Config choice (EditorPrefs) wins.
            string prefPath = EditorPrefs.GetString(ActiveConfigPref, "");
            if (!string.IsNullOrEmpty(prefPath))
                config = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(prefPath);

            // 2. Fall back to the legacy default location.
            if (config == null)
                config = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(ConfigPath);

            // 3. Last resort: any config asset in the project.
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ImasuSteamworksConfig");
                if (guids.Length > 0)
                    config = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            RebuildConfigEditor();
        }

        /// <summary>Switches the whole project to a different Active Config asset:
        /// rebuilds the editor, persists the choice, and auto-applies to every
        /// Session Manager in the open scenes.</summary>
        void SetActiveConfig(ImasuSteamworksConfig next)
        {
            if (next == config) return;
            config = next;
            RebuildConfigEditor();
            if (config != null)
                EditorPrefs.SetString(ActiveConfigPref, AssetDatabase.GetAssetPath(config));
            else
                EditorPrefs.DeleteKey(ActiveConfigPref);

            int n = AutoApplyToSceneMasters();
            ShowNotification(new GUIContent(
                n > 0
                    ? $"Active Config: {config.name} (applied to {n} Session Manager(s))"
                    : $"Active Config: {config.name}"));
        }

        void RebuildConfigEditor()
        {
            if (configEditor != null) DestroyImmediate(configEditor);
            configEditor = config != null ? UnityEditor.Editor.CreateEditor(config) : null;
            gamemodeFoldouts.Clear();
            overrideFoldouts.Clear();
            mapLists.Clear();
            teamSO = null;
            InvalidateValidation();
        }

        /// <summary>Applies pending edits from the config's serialized object and
        /// auto-pushes them to Session Managers in the open scenes (throttled so
        /// slider drags don't hammer the scene every frame).</summary>
        void CommitSerializedChanges()
        {
            if (configEditor != null && configEditor.serializedObject.hasModifiedProperties)
            {
                configEditor.serializedObject.ApplyModifiedProperties();
                InvalidateValidation();
                AutoApplyThrottled();
            }
        }

        void AutoApplyThrottled()
        {
            if (Time.realtimeSinceStartup - lastAutoApply < 1.0) return;
            lastAutoApply = Time.realtimeSinceStartup;
            AutoApplyToSceneMasters();
        }

        /// <summary>Deletes an array element, handling Unity's 'delete keeps a null
        /// element when removing the last one' quirk.</summary>
        static void DeleteArrayElement(SerializedProperty array, int index)
        {
            int before = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == before)
                array.DeleteArrayElementAtIndex(index);
        }

        // ------------------------------------------------------------------ //
        //  Main layout
        // ------------------------------------------------------------------ //

        void OnGUI()
        {
            RefreshSceneSensitiveCaches();

            GUILayout.Space(4);
            Header();
            TabBar();
            GUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            switch (tab)
            {
                case TabHome: DrawHome(); break;
                case TabProject: DrawProjectAndSession(); break;
                case TabGamemodes: DrawGamemodes(); break;
                case TabSpawns: DrawSpawnsAndTeams(); break;
                case TabAdvanced: DrawAdvanced(); break;
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            BottomBar();
            GUILayout.Space(4);
        }

        /// <summary>Scene-sensitive caches (spawn scan, master presence) reset when
        /// the active scene changes so stale data never survives a scene switch.</summary>
        void RefreshSceneSensitiveCaches()
        {
            string p = SceneManager.GetActiveScene().path;
            if (p != lastScenePath)
            {
                lastScenePath = p;
                spawnScanAt = double.MinValue;
                hasMasterValid = false;
            }
        }

        void Header()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Imasu Steamworks Control Center", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Package Manager", GUILayout.Height(22), GUILayout.Width(130)))
                ImasuPackageManager.Open();
            GUILayout.EndHorizontal();

            if (config == null)
            {
                EditorGUILayout.HelpBox("No config asset yet. Click 'Create New Config' to make one — " +
                    "the single source of truth for every multiplayer setting.", MessageType.Warning);
                if (GUILayout.Button("Create New Config", GUILayout.Height(24)))
                    CreateNewConfig();
                return;
            }

            // Addon kill-switch (one click, everywhere).
            bool was = config.addonEnabled;
            config.addonEnabled = EditorGUILayout.ToggleLeft(
                new GUIContent("  Imasu Steamworks enabled (kill-switch)",
                    "OFF suppresses the whole addon: no hard errors, no Steam init, the game runs as single-player."),
                was);
            if (was != config.addonEnabled)
                EditorUtility.SetDirty(config);

            // Active Config picker. One global config asset drives everything —
            // picking another asset switches the whole project and auto-applies.
            var next = (ImasuSteamworksConfig)EditorGUILayout.ObjectField(
                new GUIContent("Active Config",
                    "The single global config asset for the project. Pick a different asset to switch " +
                    "the whole project to it — the choice is saved and auto-applied to the Session " +
                    "Managers in the open scenes. Maps are scene names inside gameModes[], not separate configs."),
                config, typeof(ImasuSteamworksConfig), false);
            if (next != config)
                SetActiveConfig(next);

            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(config), EditorStyles.miniLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Config", GUILayout.Height(22)))
                CreateNewConfig();
            if (GUILayout.Button("Duplicate Template", GUILayout.Height(22)))
                DuplicateStarterConfig();
            GUILayout.EndHorizontal();

            if (!config.addonEnabled)
                EditorGUILayout.HelpBox("Addon is DISABLED — sessions are blocked with a soft note, all hard errors suppressed.", MessageType.Warning);
        }

        void TabBar()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (GUILayout.Button(tabNames[i], EditorStyles.toolbarButton))
                    tab = i;
            }
            GUILayout.EndHorizontal();
        }

        void BottomBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button("Apply Active Config to Session Managers", GUILayout.Height(26)))
            {
                int n = ApplyToSceneMasters();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                ShowNotification(new GUIContent($"Applied Active Config to {n} Session Manager(s)"));
            }
            if (GUILayout.Button("Add Session Manager to Scene", GUILayout.Height(26)))
                AddSessionManagerToScene();
            if (GUILayout.Button("Open Docs", GUILayout.Height(26)))
                OpenDocs();
            EditorGUILayout.EndHorizontal();

            if (config != null)
            {
                var missing = MissingBuildScenes(config);
                if (missing.Count > 0)
                    EditorGUILayout.HelpBox("Scenes not in Build Settings:\n• " + string.Join("\n• ", missing),
                        MessageType.Warning);

                if (sceneValidation.Count > 0)
                {
                    int failed = 0;
                    foreach (var kv in sceneValidation)
                        if (kv.Value.Failed) failed++;
                    EditorGUILayout.HelpBox(
                        $"Scene whitelist: {sceneValidation.Count - failed}/{sceneValidation.Count} map scene(s) valid" +
                        (failed > 0 ? $", {failed} need attention (Validate on the Gamemodes tab)" : ""),
                        failed > 0 ? MessageType.Warning : MessageType.Info);
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Home
        // ------------------------------------------------------------------ //

        void DrawHome()
        {
            EditorGUILayout.HelpBox(
                "Imasu Steamworks is the reusable Steam multiplayer stack for Unity 6: lobbies, " +
                "Mirror transport, distance voice, LAN fallback and a Control Center that owns " +
                "every setting. Add the ImasuSessionManager prefab to any scene to make it multiplayer.",
                MessageType.Info);

            Section("Getting started");
            wizardFoldout = EditorGUILayout.Foldout(wizardFoldout, "Setup checklist", true);
            if (wizardFoldout)
                DrawSetupWizard();

            Section("Quick actions");
            if (GUILayout.Button("Open Tutorial (offline docs)", GUILayout.Height(28)))
                OpenDocs();
            if (GUILayout.Button("Add Session Manager to Active Scene", GUILayout.Height(24)))
                AddSessionManagerToScene();
            if (GUILayout.Button("Create a New Multiplayer Scene", GUILayout.Height(24)))
                CreateMultiplayerScene();
            if (GUILayout.Button("Sync Build Settings from Config", GUILayout.Height(24)))
            {
                if (config != null)
                {
                    int added = SyncBuildSettings(config);
                    ShowNotification(new GUIContent($"Added {added} missing scene(s)"));
                }
            }

            Section("Links");
            GUILayout.BeginHorizontal();
            LinkButton("Website", "");
            LinkButton("Asset Store", "");
            LinkButton("Discord", "");
            GUILayout.EndHorizontal();
        }

        void DrawSetupWizard()
        {
            wizardDone[0] = config != null;
            wizardDone[1] = HasSessionManagerInScene();
            wizardDone[2] = SceneSpawnCount() > 0;
            wizardDone[3] = config != null && config.HasGameModes;
            wizardDone[4] = config != null && SceneInBuildSettings(config.mainMenuScene);

            WizardRow(0, "Create a config asset", config != null,
                () => SetActiveConfig(EnsureConfigAsset()));
            WizardRow(1, "Add the Session Manager to this scene", wizardDone[1], AddSessionManagerToScene);
            WizardRow(2, "Place at least one spawn (Point or Area)", wizardDone[2], () => AddSpawnPoint());
            WizardRow(3, "Define a gamemode with maps", wizardDone[3],
                () => tab = TabGamemodes);
            WizardRow(4, "Main menu scene is in Build Settings", wizardDone[4],
                () => SyncBuildSettings(config));
        }

        void WizardRow(int index, string label, bool done, Action onGo)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(done ? "[X]  " : "[ ]  ", GUILayout.Width(36));
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(done ? "Done" : "Go", GUILayout.Width(64), GUILayout.Height(22)))
                onGo();
            GUILayout.EndHorizontal();
        }

        static void LinkButton(string label, string url)
        {
            if (GUILayout.Button(label, GUILayout.Width(120), GUILayout.Height(24)))
            {
                if (!string.IsNullOrEmpty(url))
                    Application.OpenURL(url);
                else
                    Debug.Log($"[ControlCenter] Link '{label}' is a placeholder — set its URL.");
            }
        }

        void OpenDocs()
        {
            string path = Path.GetFullPath("Docs/index.html");
            if (File.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else
                ShowNotification(new GUIContent($"Docs not found: {path}"));
        }

        // ------------------------------------------------------------------ //
        //  Project & Session
        // ------------------------------------------------------------------ //

        void DrawProjectAndSession()
        {
            if (config == null || configEditor == null)
            {
                EditorGUILayout.HelpBox("Create a config first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "The scenes the game boots to, the lobby, and the default session values. " +
                "Maps are defined inside each Gamemode (next tab) — a map is just a scene " +
                "name + a display name, there's no per-map config to make.",
                MessageType.None);

            var so = configEditor.serializedObject;
            so.Update();

            Section("Scenes");
            EditorGUILayout.DelayedTextField(so.FindProperty("mainMenuScene"),
                new GUIContent("Main Menu Scene", "Mirror offlineScene — loaded at boot and when a session stops."));
            EditorGUILayout.DelayedTextField(so.FindProperty("lobbyScene"),
                new GUIContent("Lobby Scene", "Mirror onlineScene — the waiting room. Equal to the menu scene = one shared scene for both."));
            if (config.mainMenuScene == config.lobbyScene)
                EditorGUILayout.HelpBox("Shared — one scene acts as both menu and lobby.", MessageType.Info);

            Section("Session (defaults)");
            var maxP = so.FindProperty("maxPlayers");
            var minP = so.FindProperty("minPlayers");
            EditorGUILayout.IntSlider(maxP, 2, 32, new GUIContent("Max Players", "Soft cap on a server. Gamemodes/maps can override."));
            EditorGUILayout.IntSlider(minP, 1, Mathf.Max(2, maxP.intValue),
                new GUIContent("Min Players", "Players required before the host can start. 1 = host can start solo."));
            EditorGUILayout.DelayedTextField(so.FindProperty("lobbyName"),
                new GUIContent("Lobby Name", "Default server name shown in Game Setup."));
            EditorGUILayout.PropertyField(so.FindProperty("security"),
                new GUIContent("Security", "Who may join a hosted lobby: Invite only / Friends / Open."));
            EditorGUILayout.IntSlider(so.FindProperty("lobbyCodeLength"), 4, 12,
                new GUIContent("Lobby Code Length", "Length of generated short join codes."));

            Section("Steam (global)");
            EditorGUILayout.HelpBox("Steam identity is global — not overridable per gamemode.", MessageType.None);
            EditorGUILayout.PropertyField(so.FindProperty("steamAppId"),
                new GUIContent("Steam App ID", "480 = Spacewar (dev). Set your real App ID before public builds."));
            EditorGUILayout.PropertyField(so.FindProperty("requireSteam"),
                new GUIContent("Require Steam Client", "Require Steam to be running to initialize Steamworks."));

            CommitSerializedChanges();

            Section("Build");
            var missing = MissingBuildScenes(config);
            if (missing.Count == 0)
                EditorGUILayout.HelpBox("All configured scenes are in Build Settings.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Scenes not in Build Settings:\n• " + string.Join("\n• ", missing), MessageType.Warning);

            if (GUILayout.Button("Sync Build Settings with configured scenes", GUILayout.Height(26)))
            {
                int added = SyncBuildSettings(config);
                ShowNotification(new GUIContent($"Added {added} missing scene(s)"));
            }
            if (GUILayout.Button("Create a New Multiplayer Scene (floor + spawn + camera)", GUILayout.Height(24)))
                CreateMultiplayerScene();

            EditorGUILayout.Space();
            DrawValidationPanel();
        }

        /// <summary>Creates a fresh scene with a floor, spawn area, camera and a
        /// Session Manager — a runnable map skeleton you can then add to a gamemode.</summary>
        void CreateMultiplayerScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "NewMultiplayerScene";

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";

            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(0, 6, -10);

            var spawnGo = new GameObject("PlayerSpawns");
            var area = spawnGo.AddComponent<SpawnArea>();
            area.transform.position = new Vector3(0, 1, 0);

            AddSessionManagerToScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene });
        }

        // ------------------------------------------------------------------ //
        //  Gamemodes & Maps
        // ------------------------------------------------------------------ //

        void DrawGamemodes()
        {
            if (config == null || configEditor == null)
            {
                EditorGUILayout.HelpBox("Create a config first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Each gamemode lists its maps (a map is a scene name + a display name) and optional " +
                "overrides. Effective value = map → gamemode → global. Changes save immediately and " +
                "auto-apply to the Session Managers in the open scenes.",
                MessageType.None);

            var so = configEditor.serializedObject;
            so.Update();
            var modes = so.FindProperty("gameModes");

            if (GUILayout.Button("+ Add Gamemode", GUILayout.Height(24)))
            {
                int i = modes.arraySize;
                modes.InsertArrayElementAtIndex(i);
                var mode = modes.GetArrayElementAtIndex(i);
                mode.FindPropertyRelative("displayName").stringValue = "New Mode";
                mode.FindPropertyRelative("maps").ClearArray();
            }

            for (int i = 0; i < modes.arraySize; i++)
                DrawGameModeFoldout(modes.GetArrayElementAtIndex(i), i, modes);

            if (modes.arraySize == 0)
                EditorGUILayout.HelpBox("No gamemodes yet — use '+ Add Gamemode' above.", MessageType.Warning);

            EditorGUILayout.Space();
            DrawValidationPanel();

            CommitSerializedChanges();
        }

        /// <summary>One gamemode as a foldout row that expands in place. Reorder via ▲▼,
        /// expand via the arrow — expansion is never coupled to list selection (the old
        /// ReorderableList-selection approach made the arrow appear dead).</summary>
        void DrawGameModeFoldout(SerializedProperty mode, int index, SerializedProperty modes)
        {
            var display = mode.FindPropertyRelative("displayName");
            if (!gamemodeFoldouts.TryGetValue(index, out bool open))
                gamemodeFoldouts[index] = index == 0;
            string header = string.IsNullOrEmpty(display.stringValue) ? "Mode " + (index + 1) : display.stringValue;
            int maps = mode.FindPropertyRelative("maps").arraySize;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button("▲", GUILayout.Width(24)))
                {
                    modes.MoveArrayElement(index, index - 1);
                    gamemodeFoldouts.Clear();
                    overrideFoldouts.Clear();
                    return;
                }
            using (new EditorGUI.DisabledScope(index == modes.arraySize - 1))
                if (GUILayout.Button("▼", GUILayout.Width(24)))
                {
                    modes.MoveArrayElement(index, index + 1);
                    gamemodeFoldouts.Clear();
                    overrideFoldouts.Clear();
                    return;
                }
            open = EditorGUILayout.Foldout(open, $"{header}  ({maps} map(s))", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(68)))
            {
                DeleteArrayElement(modes, index);
                gamemodeFoldouts.Clear();
                overrideFoldouts.Clear();
                return;
            }
            GUILayout.EndHorizontal();
            gamemodeFoldouts[index] = open;

            if (open)
                DrawGameModeDetail(mode, index);

            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        void DrawGameModeDetail(SerializedProperty mode, int index)
        {
            var display = mode.FindPropertyRelative("displayName");

            EditorGUILayout.DelayedTextField(display,
                new GUIContent("Gamemode Name", "Shown in the Host Setup gamemode dropdown."));

            // Live effective summary (map → gamemode → global).
            EditorGUILayout.LabelField("Effective: " +
                $"players {config.EffectiveMaxPlayers(index, 0)} / {config.EffectiveMinPlayers(index)} · " +
                $"voice {config.EffectiveVoipMaxDistance(index):0.#}m · " +
                $"security {config.EffectiveSecurity(index)}", EditorStyles.miniLabel);

            Section("Maps");
            DrawMapListHeader();
            var ml = GetMapList(mode.FindPropertyRelative("maps"));
            ml.DoLayoutList();
            if (ml.index >= 0 && ml.index < ml.serializedProperty.arraySize)
                DrawMapDetail(ml.serializedProperty.GetArrayElementAtIndex(ml.index));

            if (!overrideFoldouts.TryGetValue(index, out bool ovOpen))
                overrideFoldouts[index] = false;
            ovOpen = EditorGUILayout.Foldout(ovOpen, "Overrides (optional)", true);
            overrideFoldouts[index] = ovOpen;
            if (ovOpen)
            {
                EditorGUILayout.HelpBox("Tick a field's checkbox to override the global default for this " +
                    "gamemode. Unticked fields inherit the Project & Session tab value.", MessageType.None);
                var ov = mode.FindPropertyRelative("overrides");
                Section("Session");
                DrawIntOverride(ov, "Max Players", "override_maxPlayers", "maxPlayers", 2, 32);
                DrawIntOverride(ov, "Min Players", "override_minPlayers", "minPlayers", 1, 32);
                DrawStringOverride(ov, "Lobby Name", "override_lobbyName", "lobbyName");
                DrawEnumOverride(ov, "Security", "override_security", "security");
                Section("Voice");
                DrawBoolOverride(ov, "VOIP Enabled", "override_voipEnabled", "voipEnabled");
                DrawBoolOverride(ov, "Distance Attenuation", "override_useDistanceAttenuation", "useDistanceAttenuation");
                DrawFloatOverride(ov, "Min Distance (m)", "override_voipMinDistance", "voipMinDistance", 0.1f, 500f);
                DrawFloatOverride(ov, "Max Distance (m)", "override_voipMaxDistance", "voipMaxDistance", 1f, 500f);
                DrawFloatOverride(ov, "Default User Volume", "override_voipUserVolumeDefault", "voipUserVolumeDefault", 0f, 2f);
                Section("Player Mover");
                DrawFloatOverride(ov, "Walk Speed", "override_walkSpeed", "walkSpeed", 0.1f, 30f);
                DrawFloatOverride(ov, "Sprint Multiplier", "override_sprintMultiplier", "sprintMultiplier", 1f, 5f);
                DrawFloatOverride(ov, "Jump Speed", "override_jumpSpeed", "jumpSpeed", 0f, 30f);
                DrawFloatOverride(ov, "Gravity", "override_gravity", "gravity", 0.1f, 30f);
                DrawFloatOverride(ov, "Mouse Sensitivity", "override_mouseSensitivity", "mouseSensitivity", 0.1f, 10f);
                Section("Transport");
                DrawIntOverride(ov, "P2P Timeout (s)", "override_p2pTimeoutSeconds", "p2pTimeoutSeconds", 5, 120);
                DrawBoolOverride(ov, "Steam Relay Fallback", "override_allowSteamRelay", "allowSteamRelay");
                DrawBoolOverride(ov, "SteamSockets (Next-gen)", "override_useNextGenSteamNetworking", "useNextGenSteamNetworking");
            }
        }

        /// <summary>Draggable map list per gamemode (keyed by property path so it survives
        /// reorders and rebuilds). Selecting a row shows its detail editor below.</summary>
        ReorderableList GetMapList(SerializedProperty maps)
        {
            string key = maps.propertyPath;
            if (!mapLists.TryGetValue(key, out var list) || list.serializedProperty != maps)
            {
                list = new ReorderableList(maps.serializedObject, maps, true, false, true, true)
                {
                    drawElementCallback = (rect, i, active, focused) =>
                        DrawMapRowCompact(rect, maps.GetArrayElementAtIndex(i)),
                    elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4,
                    headerHeight = 0,
                    onAddCallback = l =>
                    {
                        int i = l.serializedProperty.arraySize;
                        l.serializedProperty.InsertArrayElementAtIndex(i);
                        var map = l.serializedProperty.GetArrayElementAtIndex(i);
                        map.FindPropertyRelative("displayName").stringValue = "New Map";
                        map.FindPropertyRelative("sceneName").stringValue = "Game";
                        map.FindPropertyRelative("override_maxPlayers").boolValue = false;
                        map.FindPropertyRelative("note").stringValue = "";
                    }
                };
                mapLists[key] = list;
            }
            return list;
        }

        /// <summary>Labels the two map columns so "menu name" vs "scene name" is obvious.</summary>
        void DrawMapListHeader()
        {
            GUILayout.BeginHorizontal();
            float w = EditorGUIUtility.currentViewWidth - 80f;
            float nameW = Mathf.Min(220f, w * 0.42f);
            EditorGUILayout.LabelField("Menu name", EditorStyles.miniBoldLabel, GUILayout.Width(nameW));
            EditorGUILayout.LabelField("Scene name", EditorStyles.miniBoldLabel);
            GUILayout.EndHorizontal();
        }

        void DrawMapRowCompact(Rect rect, SerializedProperty map)
        {
            var displayName = map.FindPropertyRelative("displayName");
            var sceneName = map.FindPropertyRelative("sceneName");
            rect.y += 2;
            float w = rect.width;
            float nameW = Mathf.Min(220f, w * 0.42f);
            var r1 = new Rect(rect.x, rect.y, nameW, EditorGUIUtility.singleLineHeight);
            var r2 = new Rect(rect.x + nameW + 8, rect.y, w - nameW - 8, EditorGUIUtility.singleLineHeight);
            EditorGUI.DelayedTextField(r1, displayName, new GUIContent("", "Menu name shown in the map dropdown."));
            EditorGUI.DelayedTextField(r2, sceneName, new GUIContent("", "Scene name loaded by StartMatch."));
        }

        void DrawMapDetail(SerializedProperty map)
        {
            var displayName = map.FindPropertyRelative("displayName");
            var sceneName = map.FindPropertyRelative("sceneName");
            var note = map.FindPropertyRelative("note");
            var overrideCap = map.FindPropertyRelative("override_maxPlayers");
            var cap = map.FindPropertyRelative("maxPlayers");

            Section("Map detail");
            EditorGUILayout.DelayedTextField(displayName,
                new GUIContent("Menu Name", "Name shown in the map dropdown."));

            // A real scene picker — no guessing which string is the scene.
            EditorGUILayout.BeginHorizontal();
            var sceneAsset = FindSceneAsset(sceneName.stringValue);
            var picked = (SceneAsset)EditorGUILayout.ObjectField(
                new GUIContent("Scene", "The Unity scene this map loads. Picking a scene fills the scene name."),
                sceneAsset, typeof(SceneAsset), false);
            if (picked != null)
                sceneName.stringValue = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(picked));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Resolves to: " +
                (string.IsNullOrEmpty(sceneName.stringValue) ? "(no scene set)" : sceneName.stringValue),
                EditorStyles.miniLabel);

            overrideCap.boolValue = EditorGUILayout.ToggleLeft("Limit players on this map",
                overrideCap.boolValue);
            if (overrideCap.boolValue)
                cap.intValue = EditorGUILayout.IntSlider(cap.intValue, 2, 32);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Note");
            note.stringValue = EditorGUILayout.DelayedTextField(note.stringValue);
            EditorGUILayout.EndHorizontal();
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // Override rows: [✓] Field Name [control]. The checkbox overrides the global default.

        static void DrawBoolOverride(SerializedProperty ov, string label, string overrideField, string valueField)
        {
            var o = ov.FindPropertyRelative(overrideField);
            var v = ov.FindPropertyRelative(valueField);
            EditorGUILayout.BeginHorizontal();
            o.boolValue = EditorGUILayout.Toggle(
                new GUIContent("", "Override the global default for this gamemode."), o.boolValue, GUILayout.Width(20));
            using (new EditorGUI.DisabledScope(!o.boolValue))
                v.boolValue = EditorGUILayout.Toggle(label, v.boolValue);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawIntOverride(SerializedProperty ov, string label, string overrideField, string valueField, int lo, int hi)
        {
            var o = ov.FindPropertyRelative(overrideField);
            var v = ov.FindPropertyRelative(valueField);
            EditorGUILayout.BeginHorizontal();
            o.boolValue = EditorGUILayout.Toggle(
                new GUIContent("", "Override the global default for this gamemode."), o.boolValue, GUILayout.Width(20));
            using (new EditorGUI.DisabledScope(!o.boolValue))
                v.intValue = EditorGUILayout.IntSlider(label, v.intValue, lo, hi);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawFloatOverride(SerializedProperty ov, string label, string overrideField, string valueField, float lo, float hi)
        {
            var o = ov.FindPropertyRelative(overrideField);
            var v = ov.FindPropertyRelative(valueField);
            EditorGUILayout.BeginHorizontal();
            o.boolValue = EditorGUILayout.Toggle(
                new GUIContent("", "Override the global default for this gamemode."), o.boolValue, GUILayout.Width(20));
            using (new EditorGUI.DisabledScope(!o.boolValue))
                v.floatValue = EditorGUILayout.Slider(label, v.floatValue, lo, hi);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawStringOverride(SerializedProperty ov, string label, string overrideField, string valueField)
        {
            var o = ov.FindPropertyRelative(overrideField);
            var v = ov.FindPropertyRelative(valueField);
            EditorGUILayout.BeginHorizontal();
            o.boolValue = EditorGUILayout.Toggle(
                new GUIContent("", "Override the global default for this gamemode."), o.boolValue, GUILayout.Width(20));
            using (new EditorGUI.DisabledScope(!o.boolValue))
                v.stringValue = EditorGUILayout.DelayedTextField(label, v.stringValue);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawEnumOverride(SerializedProperty ov, string label, string overrideField, string valueField)
        {
            var o = ov.FindPropertyRelative(overrideField);
            var v = ov.FindPropertyRelative(valueField);
            EditorGUILayout.BeginHorizontal();
            o.boolValue = EditorGUILayout.Toggle(
                new GUIContent("", "Override the global default for this gamemode."), o.boolValue, GUILayout.Width(20));
            using (new EditorGUI.DisabledScope(!o.boolValue))
                v.enumValueIndex = EditorGUILayout.Popup(label, v.enumValueIndex, v.enumDisplayNames);
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ //
        //  Spawns & Teams
        // ------------------------------------------------------------------ //

        void DrawSpawnsAndTeams()
        {
            Scene active = SceneManager.GetActiveScene();

            if (config == null || configEditor == null)
            {
                EditorGUILayout.HelpBox("Create a config first.", MessageType.Warning);
                return;
            }

            if (string.IsNullOrEmpty(active.path))
                EditorGUILayout.HelpBox("The active scene isn't saved — spawns need a saved scene.", MessageType.Warning);

            Section("Spawns in this scene");
            RefreshSpawnScanIfStale();
            var points = spawnPoints;
            var areas = spawnAreas;
            EditorGUILayout.LabelField($"Active scene: {active.name} — {points.Length} point(s), {areas.Length} area(s).");

            if (points.Length + areas.Length == 0)
                EditorGUILayout.HelpBox("No spawns in this scene. Players can't spawn — add at least one.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox(
                    "Spawn team: 'Any (0)' lets every player spawn there; a specific team only serves that team. " +
                    "Until teams are actually assigned to players, keep spawns on Any (0).",
                    MessageType.Info);

            string[] teamOptions = TeamIdOptions();
            int[] teamValues = TeamIdValues();
            foreach (var sp in points) DrawSpawnRow(sp.name, sp.teamId, teamOptions, teamValues, v => SetSpawnTeam(sp, v));
            foreach (var ar in areas) DrawSpawnRow(ar.name, ar.teamId, teamOptions, teamValues, v => SetSpawnTeam(ar, v));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Spawn Point", GUILayout.Height(26)))
                AddSpawnPoint();
            if (GUILayout.Button("+ Add Spawn Area", GUILayout.Height(26)))
                AddSpawnArea();
            GUILayout.EndHorizontal();

            Section("Teams (global — used by every map)");
            if (config.teamConfig == null)
            {
                EditorGUILayout.HelpBox("No team config yet.", MessageType.Warning);
                if (GUILayout.Button("Create Default Team Config", GUILayout.Height(24)))
                {
                    var teamCfg = EnsureTeamConfigAsset();
                    config.teamConfig = teamCfg;
                    EditorUtility.SetDirty(config);
                }
                return;
            }

            var teamCfgObj = config.teamConfig;
            if (teamSO == null || teamSO.targetObject != teamCfgObj)
                teamSO = new SerializedObject(teamCfgObj);
            teamSO.Update();

            EditorGUILayout.HelpBox(
                "The team config lives in its own asset (Assets/Config/ImasuTeamConfig.asset) so it can be " +
                "shared across projects — editing here writes straight to that asset.",
                MessageType.None);

            var minSep = teamSO.FindProperty("minSeparation");
            minSep.floatValue = EditorGUILayout.Slider(
                new GUIContent("Min Spawn Separation (m)",
                    "Anti-clump: how far apart two players must spawn. 0 = off."),
                minSep.floatValue, 0f, 20f);

            Section("Quick set-up");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Team (FFA)", GUILayout.Height(22))) SetTeamCount(1);
            if (GUILayout.Button("2 Teams", GUILayout.Height(22))) SetTeamCount(2);
            if (GUILayout.Button("4 Teams", GUILayout.Height(22))) SetTeamCount(4);
            GUILayout.EndHorizontal();

            var teams = teamSO.FindProperty("teams");
            for (int i = 0; i < teams.arraySize; i++)
                DrawTeamRow(teams.GetArrayElementAtIndex(i), i, teams);

            if (GUILayout.Button("+ Add Team", GUILayout.Height(22)))
                teams.InsertArrayElementAtIndex(teams.arraySize);

            if (teamSO.hasModifiedProperties)
            {
                teamSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(config.teamConfig);
            }
        }

        void DrawSpawnRow(string label, int teamId, string[] options, int[] values, System.Action<int> onSet)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label);
            int picked = EditorGUILayout.IntPopup(teamId, options, values);
            if (picked != teamId) onSet(picked);
            EditorGUILayout.EndHorizontal();
        }

        static void SetSpawnTeam(SpawnPoint sp, int teamId)
        {
            Undo.RecordObject(sp, "Set spawn team");
            sp.teamId = teamId;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            ControlCenterInstance?.InvalidateSpawnScan();
        }

        static void SetSpawnTeam(SpawnArea ar, int teamId)
        {
            Undo.RecordObject(ar, "Set spawn team");
            ar.teamId = teamId;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            ControlCenterInstance?.InvalidateSpawnScan();
        }

        string[] TeamIdOptions()
        {
            var list = new List<string> { "Any (0)" };
            if (config != null && config.teamConfig != null && config.teamConfig.teams != null)
                foreach (var t in config.teamConfig.teams)
                    list.Add($"{t.name} ({t.spawnTeamId})");
            return list.ToArray();
        }

        int[] TeamIdValues()
        {
            var list = new List<int> { 0 };
            if (config != null && config.teamConfig != null && config.teamConfig.teams != null)
                foreach (var t in config.teamConfig.teams)
                    list.Add(t.spawnTeamId);
            return list.ToArray();
        }

        void DrawTeamRow(SerializedProperty team, int index, SerializedProperty teams)
        {
            var name = team.FindPropertyRelative("name");
            var color = team.FindPropertyRelative("color");
            var spawnId = team.FindPropertyRelative("spawnTeamId");

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▲", GUILayout.Width(24)))
            {
                if (index > 0) teams.MoveArrayElement(index, index - 1);
            }
            if (GUILayout.Button("▼", GUILayout.Width(24)))
            {
                if (index < teams.arraySize - 1) teams.MoveArrayElement(index, index + 1);
            }
            if (GUILayout.Button("Remove", GUILayout.Width(64)))
                DeleteArrayElement(teams, index);
            GUILayout.EndHorizontal();

            name.stringValue = EditorGUILayout.TextField("Name", name.stringValue);
            color.colorValue = EditorGUILayout.ColorField("Color", color.colorValue);
            spawnId.intValue = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Spawn Team ID (0 = any)", "teamId of the spawns this team uses."),
                spawnId.intValue));
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        static readonly Color[] TeamPalette =
        {
            new Color(0.4f, 1f, 0.45f),
            new Color(1f, 0.45f, 0.4f),
            new Color(0.4f, 0.6f, 1f),
            new Color(1f, 0.85f, 0.4f)
        };

        void SetTeamCount(int count)
        {
            if (config.teamConfig == null) return;
            Undo.RecordObject(config.teamConfig, "Set team count");
            var existing = config.teamConfig.teams ?? new ImasuTeamConfig.TeamEntry[0];
            var list = new List<ImasuTeamConfig.TeamEntry>();
            for (int i = 0; i < count; i++)
            {
                if (i < existing.Length)
                {
                    var t = existing[i];
                    t.spawnTeamId = i + 1;
                    list.Add(t);
                }
                else
                {
                    list.Add(new ImasuTeamConfig.TeamEntry
                    {
                        name = "Team " + (i + 1),
                        color = TeamPalette[Mathf.Min(i, TeamPalette.Length - 1)],
                        spawnTeamId = i + 1
                    });
                }
            }
            config.teamConfig.teams = list.ToArray();
            EditorUtility.SetDirty(config.teamConfig);
        }

        void AddSpawnPoint()
        {
            var go = new GameObject("SpawnPoint");
            go.AddComponent<SpawnPoint>();
            Undo.RegisterCreatedObjectUndo(go, "Add Spawn Point");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            InvalidateSpawnScan();
        }

        void AddSpawnArea()
        {
            var go = new GameObject("SpawnArea");
            go.AddComponent<BoxCollider>();
            go.AddComponent<SpawnArea>();
            Undo.RegisterCreatedObjectUndo(go, "Add Spawn Area");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            InvalidateSpawnScan();
        }

        int SceneSpawnCount()
        {
            RefreshSpawnScanIfStale();
            return spawnPoints.Length + spawnAreas.Length;
        }

        /// <summary>Scene-wide spawn scan, cached for 1 s (or until a scene/project
        /// change or a spawn edit) so repaints don't traverse the scene every frame.</summary>
        void RefreshSpawnScanIfStale()
        {
            Scene active = SceneManager.GetActiveScene();
            if (spawnScanScene == active.path && Time.realtimeSinceStartup - spawnScanAt < 1.0)
                return;
            spawnScanScene = active.path;
            spawnScanAt = Time.realtimeSinceStartup;
            spawnPoints = UnityEngine.Object.FindObjectsByType<SpawnPoint>(UnityEngine.FindObjectsSortMode.None);
            spawnAreas = UnityEngine.Object.FindObjectsByType<SpawnArea>(UnityEngine.FindObjectsSortMode.None);
        }

        void InvalidateSpawnScan()
        {
            spawnScanAt = double.MinValue;
            InvalidateValidation();
        }

        // ------------------------------------------------------------------ //
        //  Advanced (grouped custom inspector; gamemodes/maps live in their tab)
        // ------------------------------------------------------------------ //

        void DrawAdvanced()
        {
            if (config == null || configEditor == null)
            {
                EditorGUILayout.HelpBox("Create a config first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "The advanced view shows every remaining field of the config asset, grouped by section. " +
                "Gamemodes & Maps are edited on their own tab (the single canonical editor). " +
                "Changes commit when a field finishes (press Enter / click away).",
                MessageType.Info);

            configEditor.serializedObject.Update();
            configEditor.OnInspectorGUI();
            CommitSerializedChanges();
        }

        // ------------------------------------------------------------------ //
        //  Scene helpers
        // ------------------------------------------------------------------ //

        bool HasSessionManagerInScene()
        {
            if (!hasMasterValid)
            {
                hasMaster = UnityEngine.Object.FindObjectsByType<ImasuSteamworksMaster>(
                    UnityEngine.FindObjectsSortMode.None).Length > 0;
                hasMasterValid = true;
            }
            return hasMaster;
        }

        void AddSessionManagerToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);
            GameObject instance;
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
                instance.name = "ImasuSessionManager";
            }
            else
            {
                // Fallback: build the required components inline.
                instance = new GameObject("ImasuSessionManager");
                var manager = instance.AddComponent<ImasuNetworkManager>();
                if (config != null)
                {
                    manager.offlineScene = config.mainMenuScene;
                    manager.onlineScene = config.lobbyScene;
                    manager.matchScene = config.FirstMapScene() ?? "Game";
                }
                instance.AddComponent<Mirror.FizzySteam.FizzyFacepunch>();
                instance.AddComponent<kcp2k.KcpTransport>();
                instance.AddComponent<SteamManager>();
                var master = instance.AddComponent<ImasuSteamworksMaster>();
                if (config != null) master.config = config;
                instance.AddComponent<SteamLobbyManager>();
                var spawns = instance.AddComponent<SpawnManager>();
                if (config != null) spawns.teamConfig = config.teamConfig;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Add Session Manager");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            hasMasterValid = false;
            ShowNotification(new GUIContent("Session Manager added to scene"));
        }

        // ------------------------------------------------------------------ //
        //  Config asset helpers
        // ------------------------------------------------------------------ //

        ImasuSteamworksConfig EnsureConfigAsset()
        {
            ImasuSteamworksConfig existing =
                AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(ConfigPath);
            if (existing != null) return existing;

            string[] guids = AssetDatabase.FindAssets("t:ImasuSteamworksConfig");
            if (guids.Length > 0)
            {
                var any = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                if (any != null) return any;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets", "Config");

            var cfg = CreateInstance<ImasuSteamworksConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ImasuSteamworksControlCenter] Created config asset at {ConfigPath}");
            return cfg;
        }

        void CreateNewConfig()
        {
            string dst = EditorUtility.SaveFilePanelInProject("Create Config", "ImasuSteamworksConfig",
                "asset", "Create a fresh ImasuSteamworksConfig asset (global defaults, no gamemodes yet).",
                "Assets/Config");
            if (string.IsNullOrEmpty(dst)) return;

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets", "Config");

            var cfg = CreateInstance<ImasuSteamworksConfig>();
            AssetDatabase.CreateAsset(cfg, dst);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetActiveConfig(cfg);
            Selection.activeObject = cfg;
            Debug.Log($"[ImasuSteamworksControlCenter] Created config asset at {dst}");
        }

        void DuplicateStarterConfig()
        {
            string template = Path.GetFullPath("Packages/com.imasu.steamworks/Samples~/StarterConfig/TemplateImasuSteamworksConfig.asset");
            if (!File.Exists(template))
            {
                ShowNotification(new GUIContent("StarterConfig template not found"));
                return;
            }
            string dst = EditorUtility.SaveFilePanelInProject("Duplicate Template", "ImasuSteamworksConfig",
                "asset", "Where should the copied config live?", "Assets/Config");
            if (string.IsNullOrEmpty(dst)) return;
            File.Copy(template, Path.GetFullPath(dst), true);
            AssetDatabase.Refresh();
            var cfg = AssetDatabase.LoadAssetAtPath<ImasuSteamworksConfig>(dst);
            SetActiveConfig(cfg);
            Selection.activeObject = cfg;
        }

        static ImasuTeamConfig EnsureTeamConfigAsset()
        {
            const string path = "Assets/Config/ImasuTeamConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<ImasuTeamConfig>(path);
            if (cfg != null) return cfg;
            cfg = ScriptableObject.CreateInstance<ImasuTeamConfig>();
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        int ApplyToSceneMasters()
        {
            int count = 0;
            foreach (var master in UnityEngine.Object.FindObjectsByType<ImasuSteamworksMaster>(
                         UnityEngine.FindObjectsSortMode.None))
            {
                master.config = config;
                master.ApplyToComponents();
                EditorUtility.SetDirty(master);
                count++;
            }
            return count;
        }

        int AutoApplyToSceneMasters()
        {
            int n = ApplyToSceneMasters();
            if (n > 0)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return n;
        }

        // ------------------------------------------------------------------ //
        //  Validation
        // ------------------------------------------------------------------ //

        void InvalidateValidation()
        {
            validationStale = true;
            sceneValidation.Clear();
        }

        /// <summary>On-demand scene whitelist check: every catalog scene must contain
        /// an ImasuSessionManager and at least one spawn. Scenes are opened additively
        /// in the background, scanned, and closed — only when the user clicks Validate,
        /// never per-repaint (perf contract D24).</summary>
        Dictionary<string, SceneValidation> ValidateMapScenes()
        {
            if (!validationStale && sceneValidation.Count > 0) return sceneValidation;
            validationStale = false;
            sceneValidation.Clear();
            if (config == null) return sceneValidation;

            var names = new List<string>();
            foreach (var mode in config.gameModes)
                if (mode.maps != null)
                    foreach (var map in mode.maps)
                        if (!string.IsNullOrEmpty(map.sceneName) && !names.Contains(map.sceneName))
                            names.Add(map.sceneName);
            foreach (var map in config.maps)
                if (!string.IsNullOrEmpty(map.sceneName) && !names.Contains(map.sceneName))
                    names.Add(map.sceneName);
            if (names.Count == 0) return sceneValidation;

            string activePath = SceneManager.GetActiveScene().path;
            var toClose = new List<Scene>();
            try
            {
                foreach (string name in names)
                {
                    string path = FindScenePath(name);
                    if (path == null) continue;               // missing scene file — MissingBuildScenes flags it
                    if (string.Equals(activePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        sceneValidation[name] = ScanScene(SceneManager.GetActiveScene());
                        continue;
                    }
                    Scene sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    toClose.Add(sc);
                    sceneValidation[name] = ScanScene(sc);
                }
            }
            finally
            {
                foreach (var sc in toClose)
                    if (sc.IsValid())
                        EditorSceneManager.CloseScene(sc, true);
            }
            return sceneValidation;
        }

        static SceneValidation ScanScene(Scene scene)
        {
            bool hasMaster = false;
            int spawns = 0;
            foreach (var m in UnityEngine.Object.FindObjectsByType<ImasuSteamworksMaster>(
                         UnityEngine.FindObjectsSortMode.None))
                if (m.gameObject.scene == scene) { hasMaster = true; break; }
            foreach (var sp in UnityEngine.Object.FindObjectsByType<SpawnPoint>(
                         UnityEngine.FindObjectsSortMode.None))
                if (sp.gameObject.scene == scene) spawns++;
            foreach (var ar in UnityEngine.Object.FindObjectsByType<SpawnArea>(
                         UnityEngine.FindObjectsSortMode.None))
                if (ar.gameObject.scene == scene) spawns++;
            return new SceneValidation { hasMaster = hasMaster, spawnCount = spawns };
        }

        void DrawValidationPanel()
        {
            if (GUILayout.Button("Validate Maps (Session Manager + spawns)", GUILayout.Height(24)))
            {
                InvalidateValidation();
                var results = ValidateMapScenes();
                int failed = 0;
                foreach (var kv in results) if (kv.Value.Failed) failed++;
                ShowNotification(new GUIContent(
                    failed == 0
                        ? $"All {results.Count} map scene(s) valid"
                        : $"{failed} map scene(s) need attention"));
            }
            if (sceneValidation.Count == 0) return;

            var lines = new List<string>();
            foreach (var kv in sceneValidation)
            {
                if (!kv.Value.Failed) continue;
                lines.Add($"• {kv.Key}: {(kv.Value.hasMaster ? "" : "no Session Manager; ")}" +
                          $"{(kv.Value.spawnCount == 0 ? "no spawns" : "")}");
            }
            if (lines.Count > 0)
                EditorGUILayout.HelpBox(
                    "Maps that will fail:\n" + string.Join("\n", lines) +
                    "\nA valid map scene contains an ImasuSessionManager and at least one spawn.",
                    MessageType.Warning);
            else
                EditorGUILayout.HelpBox("All configured map scenes contain a Session Manager + spawns.", MessageType.Info);
        }

        static List<string> MissingBuildScenes(ImasuSteamworksConfig cfg)
        {
            var missing = new List<string>();

            void Check(string scene)
            {
                if (string.IsNullOrEmpty(scene)) return;
                if (FindScenePath(scene) == null) missing.Add(scene);
            }

            Check(cfg.mainMenuScene);
            Check(cfg.lobbyScene);

            foreach (var mode in cfg.gameModes)
                if (mode.maps != null)
                    foreach (var map in mode.maps)
                        Check(map.sceneName);
            foreach (var map in cfg.maps)
                Check(map.sceneName);

            return missing;
        }

        static string FindScenePath(string sceneName) => SceneCache.FindScenePath(sceneName);

        static SceneAsset FindSceneAsset(string sceneName) => SceneCache.FindSceneAsset(sceneName);

        static bool SceneInBuildSettings(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (string.Equals(Path.GetFileNameWithoutExtension(scene.path), sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static int SyncBuildSettings(ImasuSteamworksConfig cfg)
        {
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var seen = new HashSet<string>();
            foreach (var s in build)
                seen.Add(Path.GetFileNameWithoutExtension(s.path));

            var toAdd = new List<EditorBuildSettingsScene>();
            void Add(string scene)
            {
                if (string.IsNullOrEmpty(scene) || seen.Contains(scene)) return;
                string path = FindScenePath(scene);
                if (path == null) return;
                toAdd.Add(new EditorBuildSettingsScene(path, true));
                seen.Add(scene);
            }

            Add(cfg.mainMenuScene);
            Add(cfg.lobbyScene);
            foreach (var mode in cfg.gameModes)
                if (mode.maps != null)
                    foreach (var map in mode.maps)
                        Add(map.sceneName);
            foreach (var map in cfg.maps)
                Add(map.sceneName);

            foreach (EditorBuildSettingsScene sc in toAdd)
                build.Add(sc);

            EditorBuildSettings.scenes = build.ToArray();

            // Unity 6 Build Profiles are the authority for actual player builds;
            // keep the active profile's scene list in sync so a scene added here
            // is also in the build (out-of-the-box, no manual profile digging).
            MergeIntoActiveBuildProfile(toAdd);

            AssetDatabase.SaveAssets();
            return toAdd.Count;
        }

        /// <summary>Adds scenes to the active Build Profile (Unity 6) without
        /// removing anything already there. No-op when profiles are disabled.</summary>
        static void MergeIntoActiveBuildProfile(IList<EditorBuildSettingsScene> toAdd)
        {
            if (toAdd == null || toAdd.Count == 0) return;
#if UNITY_6000_0_OR_NEWER
            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (profile == null) return;
            var existing = new List<EditorBuildSettingsScene>(profile.scenes);
            var seen = new HashSet<string>();
            foreach (var s in existing)
                seen.Add(Path.GetFileNameWithoutExtension(s.path));

            bool changed = false;
            foreach (EditorBuildSettingsScene sc in toAdd)
            {
                string name = Path.GetFileNameWithoutExtension(sc.path);
                if (!seen.Contains(name))
                {
                    existing.Add(sc);
                    seen.Add(name);
                    changed = true;
                }
            }
            if (changed)
                profile.scenes = existing.ToArray();
#endif
        }

        /// <summary>Scene-name → path / SceneAsset lookup built by a SINGLE
        /// AssetDatabase.FindAssets pass and cached until the project changes.
        /// Previously every lookup re-ran a full-project scan, which was the
        /// Control Center's dominant lag source (per repaint and on a 0.5 s timer).</summary>
        static class SceneCache
        {
            static Dictionary<string, string> nameToPath;
            static Dictionary<string, SceneAsset> nameToAsset;
            static bool built;

            static void EnsureBuilt()
            {
                if (built) return;
                nameToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                nameToAsset = new Dictionary<string, SceneAsset>(StringComparer.OrdinalIgnoreCase);
                foreach (string guid in AssetDatabase.FindAssets("t:SceneAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!nameToPath.ContainsKey(name)) nameToPath[name] = path;
                }
                built = true;
            }

            public static string FindScenePath(string sceneName)
            {
                if (string.IsNullOrEmpty(sceneName)) return null;
                EnsureBuilt();
                return nameToPath.TryGetValue(sceneName, out string path) ? path : null;
            }

            public static SceneAsset FindSceneAsset(string sceneName)
            {
                if (string.IsNullOrEmpty(sceneName)) return null;
                EnsureBuilt();
                if (nameToAsset.TryGetValue(sceneName, out SceneAsset asset)) return asset;
                string path = FindScenePath(sceneName);
                asset = path != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;
                nameToAsset[sceneName] = asset;   // cache misses too; Invalidate() clears on project change
                return asset;
            }

            public static void Invalidate()
            {
                built = false;
                nameToPath = null;
                nameToAsset = null;
            }
        }
    }
}