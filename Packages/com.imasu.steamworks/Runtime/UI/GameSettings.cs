using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// PlayerPrefs-backed user settings (display / audio / game) with live
    /// apply. The UI mutates via the Set* methods; <see cref="SettingsBoot"/>
    /// applies everything once at startup so saved choices survive relaunch.
    /// </summary>
    public static class GameSettings
    {
        const string KeyDispMode = "hds_display_mode";      // 0 exclusive, 1 borderless, 2 windowed
        const string KeyResW = "hds_res_w";
        const string KeyResH = "hds_res_h";
        const string KeyVsync = "hds_vsync";                // 0/1
        const string KeyQuality = "hds_quality";
        const string KeyVolMaster = "hds_vol_master";       // 0..1
        const string KeyVolMusic = "hds_vol_music";
        const string KeyVolSfx = "hds_vol_sfx";
        const string KeyVolVoice = "hds_vol_voice";
        const string KeyVoiceEnabled = "hds_voice_enabled"; // 0/1
        const string KeyVoiceDistance = "hds_voice_dist";
        const string KeyVoiceUserVol = "hds_voice_user_vol"; // 0..2 per-user VOIP gain
        const string KeyMouseSens = "hds_mouse_sens";       // 0.2..5 multiplier
        const string KeyUseSteam = "hds_use_steam";         // 0/1 host networking

        // ---------------- Display ----------------

        public static int DisplayMode { get => PlayerPrefs.GetInt(KeyDispMode, 1); set => PlayerPrefs.SetInt(KeyDispMode, value); }
        public static int ResWidth { get => PlayerPrefs.GetInt(KeyResW, 0); set => PlayerPrefs.SetInt(KeyResW, value); }
        public static int ResHeight { get => PlayerPrefs.GetInt(KeyResH, 0); set => PlayerPrefs.SetInt(KeyResH, value); }
        public static bool Vsync { get => PlayerPrefs.GetInt(KeyVsync, 1) == 1; set => PlayerPrefs.SetInt(KeyVsync, value ? 1 : 0); }
        public static int QualityLevel { get => PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()); set => PlayerPrefs.SetInt(KeyQuality, Mathf.Max(0, value)); }

        public static void ApplyDisplay()
        {
            FullScreenMode mode = DisplayMode switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            int w = ResWidth > 0 ? ResWidth : Screen.currentResolution.width;
            int h = ResHeight > 0 ? ResHeight : Screen.currentResolution.height;

            QualitySettings.vSyncCount = Vsync ? 1 : 0;
            QualitySettings.SetQualityLevel(Mathf.Clamp(QualityLevel, 0, QualitySettings.names.Length - 1), true);
            Screen.SetResolution(w, h, mode);
        }

        // ---------------- Audio ----------------

        public static float VolumeMaster { get => PlayerPrefs.GetFloat(KeyVolMaster, 1f); set => PlayerPrefs.SetFloat(KeyVolMaster, Mathf.Clamp01(value)); }
        public static float VolumeMusic { get => PlayerPrefs.GetFloat(KeyVolMusic, 1f); set => PlayerPrefs.SetFloat(KeyVolMusic, Mathf.Clamp01(value)); }
        public static float VolumeSfx { get => PlayerPrefs.GetFloat(KeyVolSfx, 1f); set => PlayerPrefs.SetFloat(KeyVolSfx, Mathf.Clamp01(value)); }
        public static float VolumeVoice { get => PlayerPrefs.GetFloat(KeyVolVoice, 1f); set => PlayerPrefs.SetFloat(KeyVolVoice, Mathf.Clamp01(value)); }

        public static void ApplyAudio()
        {
            AudioListener.volume = VolumeMaster;
            // Music / SFX buses and VoicePlayer gain hook up as those systems exist.
        }

        // ---------------- Game ----------------

        public static bool VoiceEnabled { get => PlayerPrefs.GetInt(KeyVoiceEnabled, 1) == 1; set => PlayerPrefs.SetInt(KeyVoiceEnabled, value ? 1 : 0); }
        public static float VoiceDistance { get => PlayerPrefs.GetFloat(KeyVoiceDistance, 30f); set => PlayerPrefs.SetFloat(KeyVoiceDistance, Mathf.Clamp(value, 1f, 500f)); }

        /// <summary>
        /// Per-user VOIP gain (0..2). Players tune how loud OTHERS are — they do
        /// not control distance (the host owns that). Compensates for loud/quiet
        /// microphones. Consumed by VoicePlayer's AudioSource.volume.
        /// </summary>
        public static float VoiceUserVolume { get => PlayerPrefs.GetFloat(KeyVoiceUserVol, 1f); set => PlayerPrefs.SetFloat(KeyVoiceUserVol, Mathf.Clamp(value, 0f, 2f)); }

        /// <summary>Mouse-look sensitivity multiplier (0.2..5).</summary>
        public static float MouseSens { get => PlayerPrefs.GetFloat(KeyMouseSens, 1f); set => PlayerPrefs.SetFloat(KeyMouseSens, Mathf.Clamp(value, 0.2f, 5f)); }

        /// <summary>Host networking: true = Steam lobbies/transport, false = LAN/KCP.</summary>
        public static bool UseSteam { get => PlayerPrefs.GetInt(KeyUseSteam, 1) == 1; set => PlayerPrefs.SetInt(KeyUseSteam, value ? 1 : 0); }

        // ---------------- All ----------------

        public static void ApplyAll()
        {
            if (ResWidth <= 0 || ResHeight <= 0)
            {
                ResWidth = Screen.currentResolution.width;
                ResHeight = Screen.currentResolution.height;
            }
            ApplyDisplay();
            ApplyAudio();
        }

        public static void RestoreDefaults()
        {
            DisplayMode = 1;
            ResWidth = 0;
            ResHeight = 0;
            Vsync = true;
            QualityLevel = QualitySettings.names.Length - 1;
            VolumeMaster = 1f;
            VolumeMusic = 1f;
            VolumeSfx = 1f;
            VolumeVoice = 1f;
            VoiceEnabled = true;
            VoiceDistance = 30f;
            VoiceUserVolume = 1f;
            MouseSens = 1f;
            UseSteam = true;
            ApplyAll();
        }

        /// <summary>
        /// First-launch seeding: writes the config asset's values as PlayerPrefs
        /// defaults when the player has never touched that key. Lets the Control
        /// Center be the single source of truth without wiping a user's choices.
        /// </summary>
        public static void SeedFromConfig(ImasuSteamworksConfig cfg)
        {
            if (cfg == null) return;
            if (!PlayerPrefs.HasKey(KeyVoiceEnabled)) VoiceEnabled = cfg.voipEnabled;
            if (!PlayerPrefs.HasKey(KeyVoiceUserVol)) VoiceUserVolume = cfg.voipUserVolumeDefault;
            if (!PlayerPrefs.HasKey(KeyVoiceDistance)) VoiceDistance = cfg.voipMaxDistance;
            if (!PlayerPrefs.HasKey(KeyMouseSens)) MouseSens = cfg.mouseSensitivity;
            if (!PlayerPrefs.HasKey(KeyUseSteam)) UseSteam = cfg.allowNonSteamServers;
        }
    }

    /// <summary>Applies saved settings once per play session, before any UI reads them.</summary>
    static class SettingsBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void Init()
        {
            GameSettings.SeedFromConfig(ImasuSteamworksMaster.Config);
            GameSettings.ApplyAll();
        }
    }
}
