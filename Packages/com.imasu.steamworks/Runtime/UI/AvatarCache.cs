using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Caches Steam profile avatars as Texture2D for UI row icons. Get() returns
    /// the texture immediately on a cache hit, or kicks off an async fetch and
    /// returns null — callers simply re-ask next refresh, or subscribe to
    /// <see cref="onLoaded"/> for one-shot late assignment (invite rows etc.).
    ///
    /// Friends' avatars are NOT cached by the local Steam client until requested,
    /// so the first GetMediumAvatarAsync can return null while the download is in
    /// flight; we force the download with RequestUserInformation and retry a few
    /// times before giving up.
    /// </summary>
    public static class AvatarCache
    {
        const int MaxAttempts = 8;
        const float RetryDelay = 0.5f;

        static readonly Dictionary<ulong, Texture2D> cache = new Dictionary<ulong, Texture2D>();
        static readonly HashSet<ulong> pending = new HashSet<ulong>();
        static bool personaHookWired;

        /// <summary>Fired (main thread) when an avatar finishes loading. Late-assign UI here.</summary>
        public static event Action<ulong> onLoaded;

        static void EnsurePersonaHook()
        {
            if (personaHookWired) return;
            try
            {
                // Steam signals a persona state change once the requested user data
                // (incl. avatar) has actually been downloaded; use it to re-fetch
                // any avatar that was requested too early and came back empty.
                global::Steamworks.SteamFriends.OnPersonaStateChange += OnPersonaStateChanged;
                personaHookWired = true;
            }
            catch
            {
                // Steam not initialized yet — leave the flag false so a later
                // Get() retries the subscription once Steam is ready.
            }
        }

        static void OnPersonaStateChanged(global::Steamworks.Friend friend)
        {
            ulong id = friend.Id.Value;
            if (id == 0 || cache.ContainsKey(id)) return;
            pending.Remove(id); // let Request() run again now that data landed
            Request(id);
        }

        /// <summary>Cached avatar for a SteamID, or null while it downloads.</summary>
        public static Texture2D Get(ulong steamId)
        {
            if (steamId == 0) return null;
            EnsurePersonaHook();
            if (cache.TryGetValue(steamId, out Texture2D tex)) return tex;
            Request(steamId);
            return null;
        }

        static async void Request(ulong steamId)
        {
            if (!pending.Add(steamId)) return;
            try
            {
                global::Steamworks.SteamId id = (global::Steamworks.SteamId)steamId;

                // Ask the Steam client to fetch persona data if it does not have
                // it yet. CRITICAL: the second parameter is bRequireNameOnly —
                // passing true would fetch ONLY the name and skip the avatar,
                // leaving GetMediumAvatarAsync returning null forever (white
                // blocks). false fetches name AND avatar.
                try { global::Steamworks.SteamFriends.RequestUserInformation(id, false); } catch { }

                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    global::Steamworks.Data.Image? img =
                        await global::Steamworks.SteamFriends.GetMediumAvatarAsync(id);

                    if (!img.HasValue)
                    {
                        Debug.Log($"[AvatarCache] {steamId}: attempt {attempt}/{MaxAttempts} not ready yet.");
                        if (attempt < MaxAttempts)
                        {
                            await System.Threading.Tasks.Task.Delay(
                                (int)(RetryDelay * 1000 * attempt)); // back off a little
                            continue;
                        }
                        Debug.LogWarning($"[AvatarCache] No avatar for {steamId} after {MaxAttempts} attempts (Steam download still pending or no avatar set).");
                        break;
                    }

                    global::Steamworks.Data.Image im = img.Value;
                    Texture2D tex = new Texture2D((int)im.Width, (int)im.Height, TextureFormat.RGBA32, false)
                    {
                        name = $"avatar_{steamId}",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    tex.LoadRawTextureData(im.Data);
                    tex.Apply(false, true); // upload and make non-readable

                    cache[steamId] = tex;
                    Debug.Log($"[AvatarCache] avatar ready for {steamId} ({im.Width}x{im.Height}).");
                    onLoaded?.Invoke(steamId);
                    break;
                }
            }
            catch (Exception e)
            {
                // Steam offline / not initialized: stay silent-ish, retry naturally on next Get().
                UnityEngine.Debug.LogWarning($"[AvatarCache] Fetch failed for {steamId}: {e.Message}");
            }
            finally
            {
                pending.Remove(steamId);
            }
        }

        /// <summary>
        /// Drop on a UI row: assigns the avatar texture to a RawImage the moment it
        /// loads (or immediately if already cached), and unsubscribes on destroy.
        /// Fixes one-shot rows (invite list) that never re-ask for a null result.
        ///
        /// Also applies Steam's bottom-up UV flip (avatars display upside-down
        /// otherwise) and POLLS the cache for a few seconds when the texture is
        /// still null — so a row built before the download finished fills the
        /// instant the texture lands, with no refresh/typing needed.
        /// </summary>
        public class AvatarLateAssign : MonoBehaviour
        {
            RawImage target;
            ulong steamId;
            bool subscribed;
            float nextPoll;
            int polls;

            const int MaxPolls = 24;      // 12s at 0.5s
            const float PollInterval = 0.5f;

            /// <summary>
            /// (Re)bind this row to a SteamID. Safe to call repeatedly (reused rows
            /// swap friends on each filter refresh) — any previous subscription is
            /// dropped so stale onLoaded handlers can't leak.
            /// </summary>
            public void Init(ulong id, RawImage ri)
            {
                if (subscribed)
                {
                    onLoaded -= HandleLoaded;
                    subscribed = false;
                }

                steamId = id;
                target = ri;
                nextPoll = Time.unscaledTime + PollInterval;
                polls = 0;

                if (target == null || steamId == 0) return;

                // Steam avatar data is bottom-up; flip so it displays upright.
                target.uvRect = new Rect(0, 1, 1, -1);

                Texture2D tex = Get(steamId);
                if (tex != null)
                {
                    target.texture = tex;
                    return;
                }
                onLoaded += HandleLoaded;
                subscribed = true;
            }

            void HandleLoaded(ulong id)
            {
                if (id != steamId) return;
                onLoaded -= HandleLoaded;
                subscribed = false;
                if (target == null) return; // row destroyed while downloading
                target.texture = Get(steamId);
            }

            void Update()
            {
                // Poll-retry: catches the case where the download landed while a
                // subscribed row was inactive or the onLoaded handshake missed.
                if (target == null || steamId == 0) return;
                if (target.texture != null) return;
                if (polls >= MaxPolls) return;
                if (Time.unscaledTime < nextPoll) return;
                nextPoll = Time.unscaledTime + PollInterval;
                polls++;

                Texture2D tex = Get(steamId);
                if (tex != null) target.texture = tex;
            }

            void OnDestroy()
            {
                if (subscribed)
                {
                    onLoaded -= HandleLoaded;
                    subscribed = false;
                }
            }
        }
    }
}