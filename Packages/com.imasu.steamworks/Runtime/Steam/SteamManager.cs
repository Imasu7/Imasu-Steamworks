using Steamworks;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Owns the Facepunch Steamworks session lifecycle for this process.
    /// NOTE: the FizzyFacepunch transport also calls SteamClient.Init, and in
    /// Facepunch 2.4.1 a second Init THROWS ("already initialized") — it is
    /// NOT idempotent. Awake order between components is undefined, so this
    /// manager treats "already valid" as success instead of failure.
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        [Tooltip("Steamworks App ID. Dev default is 480 (Spacewar).")]
        public uint appId = 480;

        [Tooltip("Require a running Steam client, otherwise log a warning and continue without Steam.")]
        public bool requireSteam = true;

        public static SteamManager instance { get; private set; }

        public bool initialized { get; private set; }

        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            // Init deferred to Start() so the ImasuSteamworksMaster.Awake kill-switch
            // has definitely run before we decide whether to init Steam.
        }

        void Start()
        {
            // Addon kill-switch: skip Steam init entirely when disabled, so the
            // game runs as single-player with no Steam session and no hard errors.
            if (!ImasuSteamworksMaster.AddonEnabled)
            {
                Debug.Log("[SteamManager] Imasu Steamworks disabled — Steam not initialized.");
                return;
            }

            InitSteam();
        }

        void InitSteam()
        {
            // Someone (usually the transport) may have initialized first.
            if (SteamClient.IsValid)
            {
                initialized = true;
                Debug.Log($"[SteamManager] Steam already initialized by another component. User: {SteamClient.Name} ({SteamClient.SteamId.Value})");
                return;
            }

            try
            {
                SteamClient.Init(appId, requireSteam);
                initialized = SteamClient.IsValid;
                Debug.Log(initialized
                    ? $"[SteamManager] Steam initialized (appid {appId}). User: {SteamClient.Name} ({SteamClient.SteamId.Value})"
                    : "[SteamManager] Steam not initialized.");
            }
            catch (System.Exception e)
            {
                // Lost the init race: the client IS usable, just not ours.
                if (SteamClient.IsValid)
                {
                    initialized = true;
                    Debug.Log($"[SteamManager] Another component initialized Steam first ({e.Message.Split('\n')[0]}); using its session.");
                    return;
                }

                initialized = false;
                Debug.LogWarning($"[SteamManager] Steam init failed: {e.Message}");
            }
        }

        void OnApplicationQuit()
        {
            if (initialized)
            {
                try
                {
                    SteamClient.Shutdown();
                    Debug.Log("[SteamManager] Steam shut down.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamManager] Steam shutdown error: {e.Message}");
                }
                initialized = false;
            }
        }
    }
}