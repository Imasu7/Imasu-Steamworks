using Imasu.Steamworks;

namespace Imasu.Steamworks.Dedicated
{
    /// <summary>Runtime module contract for the Dedicated addon: core's
    /// ImasuSteamworksMaster discovers this via reflection and validates the
    /// config when the module is enabled.</summary>
    public class ImasuDedicatedAddon : IImasuModule
    {
        public string ModuleId => "com.imasu.steamworks.dedicated";
        public string DisplayName => "Dedicated Servers";
        public string Description => "Headless dedicated-server hosting. Separate from LAN.";

        public string InstallUrl =>
            "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.steamworks.dedicated#com.imasu.steamworks.dedicated@0.1.3";

        // Presence-based: running headless is gated by the -imasuDedicated flag, not the config.
        public bool IsEnabled(ImasuSteamworksConfig config) => true;

        public string Validate(ImasuSteamworksConfig config) => null;
    }
}