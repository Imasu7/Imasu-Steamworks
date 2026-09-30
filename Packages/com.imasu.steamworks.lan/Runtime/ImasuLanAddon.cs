using Imasu.Steamworks;

namespace Imasu.Steamworks
{
    /// <summary>Runtime module contract for the LAN addon: core's
    /// ImasuSteamworksMaster discovers this via reflection and validates the
    /// config when the module is enabled.</summary>
    public class ImasuLanAddon : IImasuModule
    {
        public string ModuleId => "com.imasu.steamworks.lan";
        public string DisplayName => "LAN (no Steam)";
        public string Description => "KCP/Telepathy LAN play without Steam. A separate addon from Dedicated.";

        public string InstallUrl =>
            "https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/com.imasu.steamworks.lan#com.imasu.steamworks.lan@0.1.3";

        public bool IsEnabled(ImasuSteamworksConfig config) => config != null && config.allowNonSteamServers;

        public string Validate(ImasuSteamworksConfig config)
        {
            if (config != null && config.allowNonSteamServers &&
                (config.kcpPort < 1024 || config.kcpPort > 65535))
                return "LAN play is enabled but kcpPort is outside 1024–65535.";
            return null;
        }
    }
}