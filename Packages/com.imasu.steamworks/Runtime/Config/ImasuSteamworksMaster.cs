using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Lives on the ImasuSession object. Holds the reference to the shared
    /// <see cref="ImasuSteamworksConfig"/> asset and is the single accessor the
    /// rest of the stack uses: <c>ImasuSteamworksMaster.Config</c>.
    ///
    /// On Awake it pushes config values into the live dependency components on
    /// the same GameObject (SteamManager, FizzyFacepunch, KcpTransport,
    /// ImasuNetworkManager) so the inspector fields and the config can never
    /// drift apart. It is a MonoBehaviour (not a static class) so the asset
    /// reference is visible in the Inspector and the Control Center window.
    ///
    /// Kill-switch: the addon is suppressed when either
    ///   1. <see cref="ImasuSteamworksConfig.addonEnabled"/> is false (the primary,
    ///      one-click switch in the Control Center), or
    ///   2. this component is disabled in the Inspector (per-scene safety net).
    /// Suppressed = no hard errors, no Steam init, no session blocking — the
    /// game simply runs without the addon.
    /// </summary>
    [DisallowMultipleComponent]
    public class ImasuSteamworksMaster : MonoBehaviour
    {
        [Tooltip("Instance mirror of the Active Config, wired by the Control Center. Don't edit this per scene — " +
         "edit the ONE global ImasuSteamworksConfig asset (Control Center → Active Config). Maps are scene names " +
         "inside gameModes[], not separate configs. At runtime only the first ImasuSessionManager survives " +
         "(DontDestroyOnLoad singleton), so this config applies to every scene.")]
        public ImasuSteamworksConfig config;

        static ImasuSteamworksMaster instance;
        static bool sceneSuppressed;

        /// <summary>
        /// Incremented every time <see cref="ApplyToComponents"/> pushes the config
        /// into the live components. UI that caches the map/gamemode catalog (the
        /// Host Setup dropdowns) watches this and rebuilds once per change, so a
        /// newly added map shows up without needing a scene reload.
        /// </summary>
        public static int CatalogVersion { get; private set; }

        /// <summary>Live config, or a defaults-only instance if no asset is wired yet.</summary>
        public static ImasuSteamworksConfig Config
        {
            get
            {
                if (instance != null && instance.config != null) return instance.config;
                return Defaults;
            }
        }

        public static bool HasInstance => instance != null;

        /// <summary>True when a config asset is actually wired to the master.</summary>
        public static bool HasConfig => instance != null && instance.config != null;

        /// <summary>True when the addon is running normally (config present AND enabled AND master active).</summary>
        public static bool AddonEnabled => !sceneSuppressed && HasConfig && instance.config.addonEnabled;

        /// <summary>True when the addon is present but intentionally suppressed (kill-switch or component disabled).</summary>
        public static bool AddonSuppressed => HasConfig && !AddonEnabled;

        /// <summary>True when the master exists but has no config asset wired (misconfiguration — hard error).</summary>
        public static bool Misconfigured => HasInstance && !HasConfig;

        static ImasuSteamworksConfig defaults;
        static ImasuSteamworksConfig Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = ScriptableObject.CreateInstance<ImasuSteamworksConfig>();
                    defaults.name = "DefaultImasuSteamworksConfig";
                    // Created at runtime as a plain object; no asset saved.
                    // Inspector display only — never used to actually start a
                    // session when the strict config gate is on.
                }
                return defaults;
            }
        }

        void Awake()
        {
            // Hybrid Session Manager lifetime: every multiplayer scene contains
            // its own ImasuSessionManager prefab instance (so the editor can
            // validate spawns/config per scene), but only the FIRST one survives
            // and becomes DontDestroyOnLoad — later scene instances self-destroy.
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Kill-switch: component disabled, or the config says addon off.
            if (!isActiveAndEnabled)
            {
                sceneSuppressed = true;
                return;
            }
            if (config != null && !config.addonEnabled)
            {
                sceneSuppressed = true;
                return;
            }

            GameSettings.SeedFromConfig(config);
            ApplyToComponents();
        }

        void OnEnable()
        {
            // A component that was disabled at load (Awake ran, OnEnable did not)
            // re-enters via the Inspector toggle. Re-evaluate the suppression.
            if (config != null && config.addonEnabled && isActiveAndEnabled)
                sceneSuppressed = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Push every config value into the live components on this object.
        /// Safe to call any time (Control Center calls it after edits).
        /// Logs an error (does not silently return) when no config is wired.
        /// </summary>
        public void ApplyToComponents()
        {
            if (config == null)
            {
                Debug.LogError("[ImasuSteamworksMaster] No config asset wired to this master. " +
                    "Open Imasu → Steamworks Control Center and create/assign a config, or disable " +
                    "this component to run without the addon.");
                return;
            }

            ImasuNetworkManager manager = GetComponent<ImasuNetworkManager>();
            if (manager != null)
            {
                manager.maxConnections = config.EffectiveMaxPlayers(0);
                if (!string.IsNullOrEmpty(config.mainMenuScene)) manager.offlineScene = config.mainMenuScene;
                if (!string.IsNullOrEmpty(config.lobbyScene)) manager.onlineScene = config.lobbyScene;
                if (!string.IsNullOrEmpty(config.FirstMapScene())) manager.matchScene = config.FirstMapScene();

                // Catalog: push both gamemodes and legacy maps so every resolution
                // path (setup dropdowns, browser names, in-match switch) works.
                manager.gameModes = CloneGameModes(config);
                manager.maps = CloneMaps(config.maps);
            }

            SteamManager steam = GetComponent<SteamManager>();
            if (steam != null && !steam.initialized)
            {
                steam.appId = config.steamAppId;
                steam.requireSteam = config.requireSteam;
            }

            var fizzy = GetComponent<Mirror.FizzySteam.FizzyFacepunch>();
            if (fizzy != null)
            {
                fizzy.SteamAppID = config.steamAppId;
                fizzy.Timeout = config.EffectiveP2pTimeoutSeconds(0);
                fizzy.AllowSteamRelay = config.EffectiveAllowSteamRelay(0);
                fizzy.UseNextGenSteamNetworking = config.EffectiveUseNextGenSteamNetworking(0);
                fizzy.InitFacepunch = false; // SteamManager owns initialization
            }

            var kcp = GetComponent<kcp2k.KcpTransport>();
            if (kcp != null)
            {
                kcp.Port = (ushort)config.kcpPort;
            }

            // Optional-addon validation (runtime IImasuModule contract). Modules that
            // are enabled by the config log a warning when their Validate() finds a
            // problem. Discovery is reflection-based so core compiles without addons.
            foreach (IImasuModule mod in DiscoverModules())
            {
                if (mod.IsEnabled(config))
                {
                    string problem = mod.Validate(config);
                    if (!string.IsNullOrEmpty(problem))
                        Debug.LogWarning($"[ImasuSteamworks] {mod.DisplayName} ({mod.ModuleId}): {problem}");
                }
            }

            CatalogVersion++;
        }

        static IImasuModule[] cachedModules;
        static bool modulesScanned;

        /// <summary>Finds installed addons that implement the runtime module contract
        /// (Voice/LAN/Dedicated). Cached; invalidated on assembly reload. Addons that
        /// aren't installed simply don't appear as types.</summary>
        static IImasuModule[] DiscoverModules()
        {
            if (modulesScanned) return cachedModules ?? System.Array.Empty<IImasuModule>();
            var found = new System.Collections.Generic.List<IImasuModule>();
            try
            {
                foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name.StartsWith("Unity", System.StringComparison.Ordinal) ||
                        asm.GetName().Name.StartsWith("System", System.StringComparison.Ordinal) ||
                        asm.GetName().Name == "mscorlib" || asm.GetName().Name == "netstandard")
                        continue;
                    foreach (System.Type t in asm.GetTypes())
                    {
                        if (t.IsAbstract || t.IsInterface || !typeof(IImasuModule).IsAssignableFrom(t)) continue;
                        if (System.Activator.CreateInstance(t) is IImasuModule mod)
                            found.Add(mod);
                    }
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException)
            {
                // Skip assemblies that can't fully load their types.
            }
            cachedModules = found.ToArray();
            modulesScanned = true;
            return cachedModules;
        }

        static ImasuNetworkManager.GameModeEntry[] CloneGameModes(ImasuSteamworksConfig cfg)
        {
            var list = new System.Collections.Generic.List<ImasuNetworkManager.GameModeEntry>();
            if (cfg.gameModes != null)
                foreach (var m in cfg.gameModes)
                    list.Add(new ImasuNetworkManager.GameModeEntry
                    {
                        displayName = string.IsNullOrEmpty(m.displayName) ? "Mode " + (list.Count + 1) : m.displayName,
                        maps = CloneMaps(m.maps)
                    });
            return list.ToArray();
        }

        static ImasuNetworkManager.MapEntry[] CloneMaps(ImasuSteamworksConfig.MapEntry[] src)
        {
            if (src == null || src.Length == 0) return System.Array.Empty<ImasuNetworkManager.MapEntry>();
            var list = new System.Collections.Generic.List<ImasuNetworkManager.MapEntry>();
            foreach (var m in src)
            {
                if (string.IsNullOrEmpty(m.sceneName)) continue;
                list.Add(new ImasuNetworkManager.MapEntry
                {
                    displayName = string.IsNullOrEmpty(m.displayName) ? m.sceneName : m.displayName,
                    sceneName = m.sceneName
                });
            }
            return list.ToArray();
        }
    }
}