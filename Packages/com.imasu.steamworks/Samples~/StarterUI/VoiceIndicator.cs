#if VOICE
using Imasu.Steamworks;
using UnityEngine;
using UnityEngine.UI;
#else
using UnityEngine;
using UnityEngine.UI;
#endif

namespace Imasu.Steamworks.Templates
{
    /// <summary>
    /// TEMPLATE — VoiceIndicator
    /// Tints a UI ring by a player's voice state, like the scoreboard:
    ///   green — this player is talking (or own mic live)
    ///   red   — mic confirmed failed (capture error)
    ///   grey  — unknown / idle
    ///
    /// How to use:
    ///   1. Add this to a GameObject with an Image (the ring).
    ///   2. Assign the Image to "ring".
    ///   3. Point it at a player by setting ownerSteamId (or leave the
    ///      "useLocalPlayer" flag to watch yourself).
    /// </summary>
    public class VoiceIndicator : MonoBehaviour
    {
        [Tooltip("The ring Image that gets recolored.")]
        public Image ring;

        [Tooltip("Watch the local player instead of ownerSteamId.")]
        public bool useLocalPlayer;

        [Tooltip("Steam ID of the player to watch (when not useLocalPlayer).")]
        public ulong ownerSteamId;

        static readonly Color Talking = new Color(0.4f, 1f, 0.45f, 1f);
        static readonly Color MicFailed = new Color(1f, 0.32f, 0.3f, 1f);
        static readonly Color Neutral = new Color(0.34f, 0.37f, 0.42f, 1f);

        void Update()
        {
            if (ring == null) return;

#if VOICE
            VoicePlayer target = null;
            if (useLocalPlayer)
            {
                target = FindLocalPlayer();
            }
            else if (ownerSteamId != 0)
            {
                foreach (var vp in FindObjectsByType<VoicePlayer>(FindObjectsSortMode.None))
                {
                    if (vp.OwnerSteamId == ownerSteamId) { target = vp; break; }
                }
            }

            if (target == null) { ring.color = Neutral; return; }

            if (target.micState == VoicePlayer.MicState.Failed) ring.color = MicFailed;
            else if (target.isSpeaking || target.isMicLive) ring.color = Talking;
            else ring.color = Neutral;
#else
            ring.color = Neutral;
#endif
        }

#if VOICE
        static VoicePlayer FindLocalPlayer()
        {
            foreach (var vp in FindObjectsByType<VoicePlayer>(FindObjectsSortMode.None))
                if (vp.isLocalPlayer) return vp;
            return null;
        }
#endif
    }
}