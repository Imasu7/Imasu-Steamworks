using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Imasu.Steamworks.Editor.Addons;

namespace Imasu.Steamworks.Editor
{
    /// <summary>
    /// Imasu Package Manager — installs, updates and removes every Imasu package
    /// (core, Voice, LAN, Dedicated, Character) plus the free Steam dependencies
    /// (Mirror + Facepunch transport) that Imasu Steamworks needs.
    ///
    /// Installs pull from GitHub (git URL + scoped tag) and pin that tag in
    /// Packages/manifest.json. Open via Imasu → Package Manager.
    /// </summary>
    public class ImasuPackageManager : EditorWindow
    {
        const string CatalogPath = "Packages/com.imasu.steamworks/Editor/Addons/catalog.json";

        // Installed packages / dependencies (cached; invalidated on package events).
        readonly Dictionary<string, InstalledInfo> installedModules =
            new Dictionary<string, InstalledInfo>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, InstalledInfo> installedDeps =
            new Dictionary<string, InstalledInfo>(StringComparer.OrdinalIgnoreCase);
        bool installedValid;

        List<IImasuModuleEditor> modules = new List<IImasuModuleEditor>();
        bool modulesValid;

        string filter = "";
        string lastOpError;
        double errorShownAt;

        UnityEditor.PackageManager.Requests.AddRequest pendingAdd;
        UnityEditor.PackageManager.Requests.RemoveRequest pendingRemove;
        UnityEditor.PackageManager.Requests.AddRequest pendingDepAdd;
        readonly Queue<CatalogDependency> depQueue = new Queue<CatalogDependency>();

        Vector2 scroll;

        [MenuItem("Imasu/Package Manager")]
        public static void Open()
        {
            var win = GetWindow<ImasuPackageManager>("Imasu Package Manager");
            win.minSize = new Vector2(480, 560);
            win.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            UnityEditor.PackageManager.Events.registeredPackages += OnRegisteredPackages;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            UnityEditor.PackageManager.Events.registeredPackages -= OnRegisteredPackages;
        }

        void OnEditorUpdate()
        {
            if (pendingAdd != null || pendingRemove != null || pendingDepAdd != null)
                Repaint();
        }

        void OnRegisteredPackages(PackageRegistrationEventArgs e)
        {
            installedValid = false;
            modulesValid = false;
            catalogLoaded = false;
            Repaint();
        }

        // ------------------------------------------------------------------ //
        //  Main
        // ------------------------------------------------------------------ //

        void OnGUI()
        {
            RefreshInstalled();
            PollPackageRequests();

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Imasu Package Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Imasu Steamworks ships no third-party code. The Steam dependencies (Mirror + the " +
                "Facepunch transport, both MIT) are pulled free from git; every Imasu package is " +
                "installed from the GitHub monorepo at a pinned scoped tag.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawDependencies();
            DrawError();
            DrawPackageList();

            EditorGUILayout.EndScrollView();
        }

        void DrawError()
        {
            if (string.IsNullOrEmpty(lastOpError)) return;
            // Auto-dismiss after a few seconds.
            if (Time.realtimeSinceStartup - errorShownAt > 8f)
            {
                lastOpError = null;
                return;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(lastOpError, MessageType.Error);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
                lastOpError = null;
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ //
        //  Steam dependencies
        // ------------------------------------------------------------------ //

        void DrawDependencies()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Steam dependencies (free — not sold)", EditorStyles.boldLabel);

            var deps = LoadDependencies();
            bool depPending = pendingDepAdd != null && !pendingDepAdd.IsCompleted;
            bool allPresent = true;

            foreach (var dep in deps)
            {
                bool present = installedDeps.TryGetValue(dep.id, out InstalledInfo info);
                if (!present) allPresent = false;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(dep.displayName,
                    present
                        ? (info.embedded ? $"installed (embedded, v{info.version})" : $"installed (v{info.version})")
                        : "not installed",
                    present ? EditorStyles.boldLabel : EditorStyles.label);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy URL", GUILayout.Width(74)))
                    GUIUtility.systemCopyBuffer = dep.installUrl;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(new GUIContent(dep.installUrl,
                        dep.installUrl), EditorStyles.miniLabel);
            }

            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(depPending || allPresent))
            {
                if (GUILayout.Button(allPresent
                        ? "Steam dependencies installed"
                        : "Install Steam dependencies (Mirror + Facepunch)", GUILayout.Height(24)))
                {
                    depQueue.Clear();
                    foreach (var dep in deps)
                        if (!installedDeps.ContainsKey(dep.id))
                            depQueue.Enqueue(dep);
                    PumpDepQueue();
                }
            }
            if (depPending)
                EditorGUILayout.LabelField("Installing…", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        // ------------------------------------------------------------------ //
        //  Packages
        // ------------------------------------------------------------------ //

        void DrawPackageList()
        {
            EditorGUILayout.LabelField("Packages", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            filter = EditorGUILayout.TextField(new GUIContent("Filter",
                "Filter by package id, name or description."), filter);
            if (!string.IsNullOrEmpty(filter) && GUILayout.Button("✕", GUILayout.Width(24)))
            {
                filter = "";
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();
            bool hasFilter = !string.IsNullOrWhiteSpace(filter);

            bool opPending = (pendingAdd != null && !pendingAdd.IsCompleted)
                          || (pendingRemove != null && !pendingRemove.IsCompleted);
            if (opPending)
                EditorGUILayout.HelpBox("Package operation in progress…", MessageType.Info);

            foreach (var row in LoadCatalog())
            {
                if (hasFilter && !MatchesFilter(row, filter)) continue;

                bool isCore = row.id == "com.imasu.steamworks";
                bool present = installedModules.ContainsKey(row.id) || isCore;
                bool embedded = present && !isCore && installedModules.TryGetValue(row.id, out var info) && info.embedded;
                string installedVersion = present && !isCore
                    ? (installedModules.TryGetValue(row.id, out var v) ? v.version : "")
                    : "";
                bool published = !string.IsNullOrEmpty(row.installPath) && !string.IsNullOrEmpty(row.tag);
                bool updateAvailable = published && !embedded && present && !isCore
                    && IsNewerTag(row.tag, installedVersion);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.displayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(StatusText(row, isCore, present, embedded, installedVersion, updateAvailable),
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(row.id, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(new GUIContent(row.description, row.description),
                    EditorStyles.wordWrappedLabel);

                if (isCore)
                {
                    EditorGUILayout.HelpBox("Core is always required — it ships this window.", MessageType.None);
                }
                else if (present && embedded)
                {
                    EditorGUILayout.HelpBox("Embedded in this project's Packages/ folder (dev mode) — no remove needed.", MessageType.None);
                }
                else if (present)
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(opPending))
                    {
                        if (updateAvailable && GUILayout.Button($"Update to {TagVersion(row.tag)}", GUILayout.Width(150)))
                            InstallModule(row, update: true);
                        else if (!updateAvailable)
                            EditorGUILayout.LabelField("Installed", EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                            RemoveModule(row.id);
                    }
                    if (published && GUILayout.Button("Release notes", GUILayout.Width(104)))
                        Application.OpenURL("https://github.com/Imasu7/Imasu-Steamworks/releases/tag/" + row.tag);
                    EditorGUILayout.EndHorizontal();
                }
                else if (published)
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(opPending))
                    {
                        if (GUILayout.Button($"Install {TagVersion(row.tag)}", GUILayout.Height(24)))
                            InstallModule(row, update: false);
                    }
                    if (GUILayout.Button("Copy URL", GUILayout.Width(80)))
                        CopyInstallUrl(row);
                    if (GUILayout.Button("Release notes", GUILayout.Width(104)))
                        Application.OpenURL("https://github.com/Imasu7/Imasu-Steamworks/releases/tag/" + row.tag);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Planned — not yet published (no version tag).", MessageType.None);
                }

                // Inline addon settings when an IImasuModuleEditor exists and the addon is installed.
                if (present && !isCore)
                {
                    foreach (var mod in DiscoverModuleEditors())
                    {
                        if (mod.ModuleId != row.id) continue;
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                        mod.DrawSettings();
                        break;
                    }
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(6);
            }
        }

        static string StatusText(ModuleRow row, bool isCore, bool present, bool embedded,
            string installedVersion, bool updateAvailable)
        {
            if (isCore) return "Core (always present)";
            if (!present) return "Not installed";
            if (embedded) return $"Installed (embedded, v{installedVersion})";
            return $"Installed (v{installedVersion})" + (updateAvailable ? " — update available" : "");
        }

        static bool MatchesFilter(ModuleRow row, string filter)
        {
            string hay = (row.id + " " + row.displayName + " " + row.description).ToLowerInvariant();
            return hay.IndexOf(filter.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------ //
        //  Installed package tracking
        // ------------------------------------------------------------------ //

        void RefreshInstalled()
        {
            if (installedValid) return;
            installedModules.Clear();
            installedDeps.Clear();
            foreach (var pkg in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                // Dependencies checked BEFORE the com.imasu.* prefix: com.imasu.facepunch
                // is a dependency, not a module.
                if (pkg.name == "com.mirrornetworking.mirror" || pkg.name == "com.imasu.facepunch")
                    installedDeps[pkg.name] = new InstalledInfo
                    {
                        embedded = pkg.source == PackageSource.Embedded,
                        version = pkg.version
                    };
                else if (pkg.name.StartsWith("com.imasu.", StringComparison.OrdinalIgnoreCase))
                    installedModules[pkg.name] = new InstalledInfo
                    {
                        embedded = pkg.source == PackageSource.Embedded,
                        version = pkg.version
                    };
            }
            installedValid = true;
        }

        List<IImasuModuleEditor> DiscoverModuleEditors()
        {
            if (modulesValid) return modules;
            modules.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IImasuModuleEditor>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (Activator.CreateInstance(type) is IImasuModuleEditor m)
                    modules.Add(m);
            }
            modulesValid = true;
            return modules;
        }

        struct InstalledInfo
        {
            public bool embedded;
            public string version;
        }

        // ------------------------------------------------------------------ //
        //  Install / Remove / Update
        // ------------------------------------------------------------------ //

        void InstallModule(ModuleRow row, bool update)
        {
            var missing = MissingDependencies();
            if (missing.Count > 0)
            {
                string names = string.Join(", ", missing.ConvertAll(d => d.displayName));
                if (EditorUtility.DisplayDialog("Missing Steam dependencies",
                        $"{row.displayName} needs {names}, which aren't installed yet.\n\n" +
                        "Install them now (free, from git)?", "Install dependencies", "Cancel"))
                {
                    foreach (var dep in missing) depQueue.Enqueue(dep);
                    PumpDepQueue();
                }
                return;
            }

            string url = GitUrl(row);
            string verb = update ? "Update" : "Install";
            if (!EditorUtility.DisplayDialog(verb + " package",
                    $"{verb} {row.displayName} ({row.id})?\n\n{url}", verb, "Cancel"))
                return;
            lastOpError = null;
            pendingAdd = UnityEditor.PackageManager.Client.Add(url);
        }

        void RemoveModule(string id)
        {
            if (!EditorUtility.DisplayDialog("Remove package",
                    $"Remove {id} from this project?\n\nThis edits Packages/manifest.json.", "Remove", "Cancel"))
                return;
            lastOpError = null;
            pendingRemove = UnityEditor.PackageManager.Client.Remove(id);
        }

        void PumpDepQueue()
        {
            if (pendingDepAdd != null && !pendingDepAdd.IsCompleted) return;
            if (depQueue.Count == 0) return;
            var dep = depQueue.Dequeue();
            Debug.Log($"[ImasuPackageManager] Installing dependency {dep.id} from {dep.installUrl}");
            lastOpError = null;
            pendingDepAdd = UnityEditor.PackageManager.Client.Add(dep.installUrl);
        }

        List<CatalogDependency> MissingDependencies()
        {
            var missing = new List<CatalogDependency>();
            foreach (var dep in LoadDependencies())
                if (!installedDeps.ContainsKey(dep.id))
                    missing.Add(dep);
            return missing;
        }

        /// <summary>Package ops are async — poll them in OnGUI (Unity 6 UPM exposes
        /// IsCompleted/Status/Error, not a Completed event).</summary>
        void PollPackageRequests()
        {
            bool done = false;

            if (pendingDepAdd != null && pendingDepAdd.IsCompleted)
            {
                var r = pendingDepAdd;
                pendingDepAdd = null;
                done = true;
                if (r.Status == StatusCode.Success)
                {
                    lastOpError = null;
                    installedValid = false;
                }
                else
                {
                    ReportError("Installing a Steam dependency failed", r.Error);
                }
                PumpDepQueue(); // continue the chain, if any
            }

            if (pendingAdd != null && pendingAdd.IsCompleted)
            {
                var r = pendingAdd;
                pendingAdd = null;
                done = true;
                if (r.Status == StatusCode.Success)
                {
                    lastOpError = null;
                    installedValid = false;
                    modulesValid = false;
                    RefreshInstalled();
                    ShowNotification(new GUIContent("Package installed"));
                }
                else
                {
                    ReportError("Install failed", r.Error);
                }
            }

            if (pendingRemove != null && pendingRemove.IsCompleted)
            {
                var r = pendingRemove;
                pendingRemove = null;
                done = true;
                if (r.Status == StatusCode.Success)
                {
                    lastOpError = null;
                    installedValid = false;
                    RefreshInstalled();
                    ShowNotification(new GUIContent("Package removed"));
                }
                else
                {
                    ReportError("Remove failed", r.Error);
                }
            }

            if (done) Repaint();
        }

        void ReportError(string context, UnityEditor.PackageManager.Error error)
        {
            lastOpError = $"{context}: {error?.message ?? "unknown error"}";
            errorShownAt = Time.realtimeSinceStartup;
            EditorUtility.DisplayDialog("Imasu Package Manager", lastOpError, "OK");
        }

        // ------------------------------------------------------------------ //
        //  Catalog
        // ------------------------------------------------------------------ //

        static string GitUrl(ModuleRow row) =>
            "https://github.com/Imasu7/Imasu-Steamworks.git?path=" + row.installPath + "#" + row.tag;

        static string TagVersion(string tag)
        {
            int at = tag.IndexOf('@');
            return at >= 0 ? tag.Substring(at + 1) : tag;
        }

        static bool IsNewerTag(string catalogTag, string installedVersion)
        {
            if (string.IsNullOrEmpty(installedVersion)) return false;
            return VersionCompare(TagVersion(catalogTag), installedVersion) > 0;
        }

        static int VersionCompare(string a, string b)
        {
            string[] pa = a.Split('.');
            string[] pb = b.Split('.');
            int n = Mathf.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out int xv) ? xv : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out int yv) ? yv : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        static void CopyInstallUrl(ModuleRow row)
        {
            GUIUtility.systemCopyBuffer = GitUrl(row);
        }

        [Serializable]
        class ModuleRow
        {
            public string id;
            public string displayName;
            public string description;
            public string installPath;
            public string tag;
        }

        [Serializable]
        class CatalogDependency
        {
            public string id;
            public string displayName;
            public string description;
            public string installUrl;
        }

        [Serializable]
        class CatalogData
        {
            public ModuleRow[] modules;
            public CatalogDependency[] dependencies;
        }

        static ModuleRow[] cachedCatalog;
        static CatalogDependency[] cachedDependencies = new CatalogDependency[0];
        static bool catalogLoaded;

        static ModuleRow[] LoadCatalog()
        {
            if (catalogLoaded) return cachedCatalog;
            string path = Path.GetFullPath(CatalogPath);
            if (File.Exists(path))
            {
                try
                {
                    var data = JsonUtility.FromJson<CatalogData>(File.ReadAllText(path));
                    if (data != null && data.modules != null && data.modules.Length > 0)
                    {
                        cachedCatalog = data.modules;
                        cachedDependencies = data.dependencies ?? new CatalogDependency[0];
                        catalogLoaded = true;
                        return cachedCatalog;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ImasuPackageManager] Failed to read catalog ({path}): {e.Message}");
                }
            }
            cachedCatalog = ModuleCatalogFallback;
            cachedDependencies = CatalogDependencyFallback;
            catalogLoaded = true;
            return cachedCatalog;
        }

        static CatalogDependency[] LoadDependencies()
        {
            LoadCatalog();
            return cachedDependencies;
        }

        static readonly CatalogDependency[] CatalogDependencyFallback =
        {
            new CatalogDependency
            {
                id = "com.mirrornetworking.mirror",
                displayName = "Mirror (MIT)",
                description = "High-level Unity networking library. Mirror's own repo has no UPM package.json (unitypackage/Asset Store only), so this free git-path shim hosts the vendored MIT v96.11.0 source. Not sold.",
                installUrl = "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0"
            },
            new CatalogDependency
            {
                id = "com.imasu.facepunch",
                displayName = "Facepunch + FizzyFacepunch (MIT)",
                description = "Steam API wrapper DLLs + the Mirror Steam transport. Free repackage; not sold.",
                installUrl = "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.facepunch#com.imasu.facepunch@0.1.1"
            }
        };

        static readonly ModuleRow[] ModuleCatalogFallback =
        {
            new ModuleRow
            {
                id = "com.imasu.steamworks",
                displayName = "Core",
                description = "Config, Steam lobbies, Session Manager, Control Center. Always required.",
                installPath = "Packages/com.imasu.steamworks", tag = "com.imasu.steamworks@0.1.3"
            },
            new ModuleRow
            {
                id = "com.imasu.steamworks.voice",
                displayName = "Voice",
                description = "Distance VOIP (Steam voice codec). Optional addon - install only if you want voice.",
                installPath = "Packages/com.imasu.steamworks.voice", tag = "com.imasu.steamworks.voice@0.1.3"
            },
            new ModuleRow
            {
                id = "com.imasu.steamworks.lan",
                displayName = "LAN (no Steam)",
                description = "KCP/Telepathy LAN play without Steam. A separate addon from Dedicated.",
                installPath = "Packages/com.imasu.steamworks.lan", tag = "com.imasu.steamworks.lan@0.1.3"
            },
            new ModuleRow
            {
                id = "com.imasu.steamworks.dedicated",
                displayName = "Dedicated Servers",
                description = "Headless dedicated-server hosting. Separate from LAN.",
                installPath = "Packages/com.imasu.steamworks.dedicated", tag = "com.imasu.steamworks.dedicated@0.1.3"
            },
            new ModuleRow
            {
                id = "com.imasu.character",
                displayName = "Character Controller",
                description = "Advanced first-person character controller (separate module, planning).",
                installPath = "Packages/com.imasu.character", tag = ""
            }
        };
    }
}