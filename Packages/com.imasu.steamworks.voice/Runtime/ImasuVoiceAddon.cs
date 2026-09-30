using Imasu.Steamworks;

namespace Imasu.Steamworks
{
    /// <summary>Runtime module contract for the Voice addon: core's
    /// ImasuSteamworksMaster discovers this via reflection and validates the
    /// config when the module is enabled.</summary>
    public class ImasuVoiceAddon : IImasuModule
    {
        public string ModuleId => "com.imasu.steamworks.voice";
        public string DisplayName => "Voice";
        public string Description => "Distance VOIP (Steam voice codec). Optional addon — install only if you want voice.";

        public string InstallUrl =>
            "https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/com.imasu.steamworks.voice#com.imasu.steamworks.voice@0.1.3";

        public bool IsEnabled(ImasuSteamworksConfig config) => config != null && config.voipEnabled;

        public string Validate(ImasuSteamworksConfig config)
        {
            if (config != null && config.voipEnabled && config.useDistanceAttenuation &&
                config.voipMaxDistance < config.voipMinDistance)
                return "Distance voice is enabled but voipMaxDistance is below voipMinDistance.";
            return null;
        }
    }
}