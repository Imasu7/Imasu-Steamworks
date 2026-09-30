using Imasu.Steamworks;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — HostSetupForm
    /// Binds the shared ImasuSteamworksConfig to a host-setup UI so a new dev can
    /// expose max/min players, map, security and voice without writing logic.
    ///
    /// How to use:
    ///   1. Add this to your host-setup screen.
    ///   2. Wire the optional slots (sliders/dropdowns) below, or just read
    ///      Config values directly in your own code.
    ///   3. When the host creates a session, your code can read
    ///      ImasuSteamworksMaster.Config (e.g. maxPlayers, security, maps).
    /// </summary>
    public class HostSetupForm : MonoBehaviour
    {
        [Tooltip("Optional slider bound to maxPlayers (2..32).")]
        public Slider maxPlayersSlider;

        [Tooltip("Optional dropdown for security: 0=Invite only, 1=Friends, 2=Open.")]
        public Dropdown securityDropdown;

        /// <summary>Live config (falls back to defaults if no asset is wired).</summary>
        public ImasuSteamworksConfig Config => ImasuSteamworksMaster.Config;

        void Start()
        {
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.minValue = 2;
                maxPlayersSlider.maxValue = 32;
                maxPlayersSlider.wholeNumbers = true;
                maxPlayersSlider.value = Config.maxPlayers;
                maxPlayersSlider.onValueChanged.AddListener(v => Config.maxPlayers = Mathf.RoundToInt(v));
            }

            if (securityDropdown != null)
            {
                securityDropdown.value = (int)Config.security;
                securityDropdown.onValueChanged.AddListener(v =>
                    Config.security = (LobbySecurity)Mathf.Clamp(v, 0, 2));
            }
        }
    }
}