namespace Imasu.Steamworks
{
    /// <summary>
    /// Optional-addon contract (Control Center 2.0 / modular packages). Each
    /// optional package (Voice, LAN, Dedicated, …) ships one implementation so
    /// the Control Center can list it, gate its settings, and validate its
    /// presence — even though the core package compiles without it.
    ///
    /// The Control Center discovers implementations via
    /// TypeCache.GetTypesDerivedFrom (Editor) or AppDomain (runtime fallback).
    /// A module that isn't installed simply doesn't exist as a type, so core
    /// never hard-depends on it.
    /// </summary>
    public interface IImasuModule
    {
        /// <summary>UPM package name, e.g. "com.imasu.steamworks.voice".</summary>
        string ModuleId { get; }

        /// <summary>Friendly name shown in the Modules tab, e.g. "Voice".</summary>
        string DisplayName { get; }

        /// <summary>Short description for the catalog row.</summary>
        string Description { get; }

        /// <summary>GitHub install URL (monorepo path+tag) or "" if core-bundled.</summary>
        string InstallUrl { get; }

        /// <summary>True when the module's feature should be active for this config.</summary>
        bool IsEnabled(ImasuSteamworksConfig config);

        /// <summary>Returns an error string when the module is installed but misconfigured, else null.</summary>
        string Validate(ImasuSteamworksConfig config);
    }
}