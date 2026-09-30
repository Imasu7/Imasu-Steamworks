using System.Collections;
using Imasu.Steamworks;
using UnityEngine;

namespace Imasu.Steamworks.Dedicated
{
    /// <summary>
    /// Headless dedicated-server host. Activated automatically when the build is run
    /// with the <c>-imasuDedicated</c> command-line flag (no scene wiring needed):
    /// it loads the config from Resources (bundled by DedicatedBuildHelper) and starts
    /// a server-only KCP session — no Steam session, no UI.
    /// </summary>
    [AddComponentMenu("Imasu Steamworks/Dedicated Host")]
    [DisallowMultipleComponent]
    public sealed class DedicatedHost : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                return System.Array.IndexOf(args, "-imasuDedicated") >= 0;
            }
        }

        [Tooltip("Server listen port. 0 = keep the KcpTransport default.")]
        public int port = 0;

        [Tooltip("Config asset under Assets/Resources (copied there by DedicatedBuildHelper).")]
        public string configResource = "Config/ImasuSteamworksConfig";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoBootstrap()
        {
            if (!Requested) return;
            var go = new GameObject("DedicatedHost");
            go.AddComponent<DedicatedHost>();
            DontDestroyOnLoad(go);
        }

        IEnumerator Start()
        {
            var net = FindFirstObjectByType<ImasuNetworkManager>();
            if (net == null)
            {
                Debug.LogError("[Dedicated] No ImasuNetworkManager found — add the ImasuSessionManager prefab to the boot scene.");
                yield break;
            }

            var cfg = Resources.Load<ImasuSteamworksConfig>(configResource);
            var master = net.GetComponent<ImasuSteamworksMaster>();
            if (cfg != null && master != null)
            {
                master.config = cfg;
                master.ApplyToComponents();
                Debug.Log($"[Dedicated] Loaded config '{cfg.name}' from Resources/{configResource}.");
            }
            else if (master != null)
            {
                Debug.LogWarning($"[Dedicated] No config at Resources/{configResource} — using the scene-wired config.");
            }

            // CLI / environment overrides (win over the config). Env fallbacks let a
            // Docker/container pass settings without touching the command line.
            int cliPort = ReadIntArg("IMASU_PORT", "--port", "-port");
            if (cliPort > 0) port = cliPort;

            int maxPlayers = ReadIntArg("IMASU_MAX_PLAYERS", "--maxPlayers", "-maxPlayers");
            if (maxPlayers > 0) net.maxConnections = Mathf.Clamp(maxPlayers, 1, 64);

            string map = ReadStringArg("IMASU_MAP", "--map", "-map");
            if (!string.IsNullOrEmpty(map)) net.matchScene = map;

            if (cliPort > 0 || maxPlayers > 0 || !string.IsNullOrEmpty(map))
                Debug.Log($"[Dedicated] Overrides — port={port} maxPlayers={net.maxConnections} map={net.matchScene}");

            net.StartDedicatedServer(port);
        }

        static int ReadIntArg(string envName, params string[] aliases)
        {
            string env = System.Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(env) && int.TryParse(env, out int e)) return e;
            string arg = ArgValue(aliases);
            return arg != null && int.TryParse(arg, out int r) ? r : -1;
        }

        static string ReadStringArg(string envName, params string[] aliases)
        {
            string env = System.Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(env)) return env;
            return ArgValue(aliases);
        }

        static string ArgValue(params string[] aliases)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                foreach (string a in aliases)
                    if (string.Equals(args[i], a, System.StringComparison.OrdinalIgnoreCase))
                        return args[i + 1];
            return null;
        }
    }
}