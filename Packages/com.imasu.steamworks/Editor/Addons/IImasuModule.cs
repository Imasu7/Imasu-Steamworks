namespace Imasu.Steamworks.Editor.Addons
{
    /// <summary>
    /// Editor-side surface for an addon shown in the Control Center Modules tab.
    /// Install / Remove / Update buttons are driven by the ModuleCatalog in the
    /// Control Center; implementations here only draw inline settings when the
    /// addon is installed. The runtime-side contract (enabled/validation per
    /// config) is <see cref="Imasu.Steamworks.IImasuModule"/>.
    /// </summary>
    public interface IImasuModuleEditor
    {
        /// <summary>Package id, e.g. "com.imasu.steamworks.voice" — must match ModuleCatalog.</summary>
        string ModuleId { get; }

        /// <summary>Human name shown in the Modules tab.</summary>
        string DisplayName { get; }

        /// <summary>Drawn inline in the Modules tab when the addon is installed.</summary>
        void DrawSettings();
    }
}