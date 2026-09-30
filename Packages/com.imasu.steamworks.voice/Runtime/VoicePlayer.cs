using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Distance-based Steam voice (Slice 6).
    ///
    /// Capture (local player): push-to-talk sets SteamUser.VoiceRecord; each frame
    /// any new compressed voice bytes are sent over Mirror's UNRELIABLE channel
    /// (low latency, never the reliable channel).
    ///
    /// Playback (remote players): compressed bytes are decompressed to 16-bit PCM
    /// via SteamUser.DecompressVoice and fed into a streamed AudioClip on a 3D
    /// AudioSource. Unity handles distance/rolloff, so a speaker further than
    /// voiceRange is simply inaudible. Steam provides no proximity model — the
    /// engine does it.
    /// </summary>
    public class VoicePlayer : NetworkBehaviour, IVoiceMember
    {
        const int SampleRate = 48000; // Steam voice decompresses to 48 kHz mono PCM

        /// <summary>Own mic health as seen by the owner; broadcast for scoreboards.</summary>
        public enum MicState : byte { Unknown, Ready, Failed }

        [Header("Voice")]
        [Tooltip("Max distance (m) at which this player can be heard. Overridden by LobbySettings.voiceRange at match start.")]
        public float voiceRange = 30f;

        [Tooltip("Min distance (m) before rolloff begins.")]
        public float voiceMinDistance = 1f;

        [Tooltip("Hold to talk. If false, use TogglePushToTalk().")]
        public bool pushToTalk;

        [Tooltip("3D AudioSource used to play this player's voice (auto-created if empty).")]
        public AudioSource audioSource;

        [Tooltip("Log TX/RX voice traffic to the console (throttled).")]
        public bool logTraffic = true;

        [Header("Status (read only)")]
        [Tooltip("Steam ID of this player's owner, set by the server from the transport address.")]
        [SyncVar] ulong ownerSteamId;

        /// <summary>
        /// Whether this player's mic is known to work. Set by the owner: Ready after
        /// the first successful captured voice frame, Failed after a capture error.
        /// Stays Unknown until one or the other happens (confirmed-failure only).
        /// </summary>
        [SyncVar] public MicState micState = MicState.Unknown;

        // capture
        bool recording;

        // activity tracking (unscaled time)
        float lastSendActivity;
        float lastRecvActivity;
        ulong bytesSentTotal;
        ulong bytesRecvTotal;
        float nextTxLogTime;
        float nextRxLogTime;

        const float ActiveWindow = 0.5f;
        const float LogInterval = 2f;

        /// <summary>True while OUR mic is producing data that is being sent.</summary>
        public bool isMicLive => isLocalPlayer && recording && Time.unscaledTime - lastSendActivity < ActiveWindow;

        /// <summary>True while this REMOTE player's voice packets are arriving.</summary>
        public bool isSpeaking => !isLocalPlayer && Time.unscaledTime - lastRecvActivity < ActiveWindow;

        /// <summary>Total compressed voice bytes sent by the local player.</summary>
        public ulong bytesSent => bytesSentTotal;

        /// <summary>Total compressed voice bytes received for this player.</summary>
        public ulong bytesReceived => bytesRecvTotal;

        /// <summary>Steam ID of this player's owner (0 in KCP local tests).</summary>
        public ulong OwnerSteamId => ownerSteamId;

        // IVoiceMember (core can query voice state without referencing this type)
        public bool IsLocalPlayer => isLocalPlayer;
        public bool MicLive => isMicLive;
        public bool Speaking => isSpeaking;
        public bool MicFailed => micState == MicState.Failed;
        public string DisplayName => GetDisplayName();
        public long BytesSent => (long)bytesSentTotal;
        public long BytesReceived => (long)bytesRecvTotal;

        // playback
        readonly object pcmLock = new object();
        readonly Queue<float> pcmQueue = new Queue<float>(SampleRate * 2);
        float[] mixBuffer = new float[4096];
        AudioClip clip;

        void Start()
        {
            ApplyVoiceRange(); // read lobby setting first so AudioSource gets it
            EnsureAudioSource();
        }

        void OnDestroy()
        {
            StopRecording();
            if (audioSource != null) audioSource.Stop();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (connectionToClient == null) return;

            if (connectionToClient.connectionId == 0)
            {
                // Host's own player.
                try { if (SteamClient.IsValid) ownerSteamId = SteamClient.SteamId.Value; } catch { }
                return;
            }

            // FizzyFacepunch reports the remote user's Steam ID as the connection
            // address; KCP local tests report "localhost" and stay 0 on purpose.
            if (ulong.TryParse(connectionToClient.address, out ulong sid))
                ownerSteamId = sid;
        }

        // ------------------------------------------------------------------ //
        //  Capture (local player only)
        // ------------------------------------------------------------------ //

        public void SetPushToTalk(bool held)
        {
            if (!isLocalPlayer) return;
            if (held == recording) return;
            recording = held;
            try
            {
                SteamUser.VoiceRecord = held;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoicePlayer] SteamUser.VoiceRecord failed: {e.Message}");
                recording = false;
            }
        }

        public void TogglePushToTalk() => SetPushToTalk(!recording);

        void Update()
        {
            if (!isLocalPlayer || !recording || !isOwned) return;
            if (!ImasuSteamworksMaster.Config.voipEnabled || !GameSettings.VoiceEnabled)
            {
                // Master kill-switch: drop out of PTT and stop capturing.
                SetPushToTalk(false);
                return;
            }

            try
            {
                if (SteamUser.HasVoiceData)
                {
                    byte[] compressed = SteamUser.ReadVoiceDataBytes();
                    if (compressed != null && compressed.Length > 0)
                    {
                        CmdSendVoice(compressed);
                        OnVoiceSent(compressed.Length);
                        if (micState != MicState.Ready) micState = MicState.Ready;
                    }
                }
            }
            catch (Exception e)
            {
                if (micState != MicState.Failed) micState = MicState.Failed;
                Debug.LogWarning($"[VoicePlayer] capture error: {e.Message}");
            }
        }

        void StopRecording()
        {
            if (!recording) return;
            recording = false;
            try { SteamUser.VoiceRecord = false; } catch { }
        }

        // ------------------------------------------------------------------ //
        //  Mirror: send + relay on unreliable channel
        // ------------------------------------------------------------------ //

        [Command(channel = Channels.Unreliable)]
        void CmdSendVoice(byte[] compressed)
        {
            RpcReceiveVoice(compressed);
        }

        [ClientRpc(channel = Channels.Unreliable)]
        void RpcReceiveVoice(byte[] compressed)
        {
            if (isLocalPlayer) return; // don't hear yourself
            OnVoiceReceived(compressed);
            PlayCompressedVoice(compressed);
        }

        // ------------------------------------------------------------------ //
        //  Playback (remote players)
        // ------------------------------------------------------------------ //

        void PlayCompressedVoice(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0) return;

            try
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    int written = SteamUser.DecompressVoice(compressed, outStream);
                    if (written <= 0) return;
                    outStream.Position = 0;

                    // 16-bit PCM mono -> float samples
                    lock (pcmLock)
                    {
                        byte[] pcm = outStream.ToArray();
                        int sampleCount = pcm.Length / 2;
                        for (int i = 0; i < sampleCount; i++)
                        {
                            short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                            pcmQueue.Enqueue(s / 32768f);
                        }
                        // bound the queue: drop oldest if it grows huge
                        while (pcmQueue.Count > SampleRate * 5)
                            pcmQueue.Dequeue();
                    }
                }

                EnsureAudioSource();
                if (!audioSource.isPlaying) audioSource.Play();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoicePlayer] playback error: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Activity tracking + diagnostics (voice indicators / logs)
        // ------------------------------------------------------------------ //

        void OnVoiceSent(int byteCount)
        {
            lastSendActivity = Time.unscaledTime;
            bytesSentTotal += (ulong)byteCount;

            if (!logTraffic || Time.unscaledTime < nextTxLogTime) return;
            nextTxLogTime = Time.unscaledTime + LogInterval;
            Debug.Log($"[Voice] TX {byteCount} B (total {bytesSentTotal / 1024f:0.#} KB)");
        }

        void OnVoiceReceived(byte[] compressed)
        {
            int byteCount = compressed?.Length ?? 0;
            if (byteCount == 0) return;

            lastRecvActivity = Time.unscaledTime;
            bytesRecvTotal += (ulong)byteCount;

            if (!logTraffic || Time.unscaledTime < nextRxLogTime) return;
            nextRxLogTime = Time.unscaledTime + LogInterval;
            Debug.Log($"[Voice] RX {byteCount} B from {GetDisplayName()} (total {bytesRecvTotal / 1024f:0.#} KB)");
        }

        /// <summary>Human-friendly name: Steam persona when known, netId otherwise.</summary>
        public string GetDisplayName()
        {
            if (isLocalPlayer)
            {
                try { if (SteamClient.IsValid) return SteamClient.Name; } catch { }
                return "You";
            }

            if (ownerSteamId != 0 && SteamLobbyManager.instance != null &&
                SteamLobbyManager.instance.members.TryGetValue((SteamId)ownerSteamId, out string name))
                return name;

            uint netId = netIdentity != null ? netIdentity.netId : 0;
            return $"Player {netId}";
        }

        // ------------------------------------------------------------------ //
        //  AudioSource setup
        // ------------------------------------------------------------------ //

        void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (clip == null)
            {
                clip = AudioClip.Create("Voice_" + gameObject.name, SampleRate * 2, 1, SampleRate, true, OnAudioRead);
                audioSource.clip = clip;
            }

            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;

            // Fixed = no distance dropoff (2D, everyone full volume).
            // Range = logarithmic rolloff between minDistance and voiceRange.
            audioSource.spatialBlend = cfg.UseDistanceVoice ? 1f : 0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = cfg.UseDistanceVoice ? cfg.voipMinDistance : 0f;
            audioSource.maxDistance = voiceRange;

            // Master kill-switch + per-user VOIP gain (players tune how loud
            // OTHERS are, not the distance).
            bool voipActive = cfg.voipEnabled && GameSettings.VoiceEnabled;
            audioSource.volume = voipActive ? GameSettings.VoiceUserVolume : 0f;

            audioSource.loop = true;                  // keep reading from the live clip
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        void ApplyVoiceRange()
        {
            // Host's LobbySettings.voiceRange wins; otherwise the config's max.
            SteamLobbyManager lobby = SteamLobbyManager.instance;
            if (lobby != null && lobby.settings.voiceRange > 0)
                voiceRange = lobby.settings.voiceRange;
            else
                voiceRange = ImasuSteamworksMaster.Config.voipMaxDistance;
        }

        void OnAudioRead(float[] data)
        {
            lock (pcmLock)
            {
                int n = Math.Min(data.Length, pcmQueue.Count);
                for (int i = 0; i < n; i++)
                    data[i] = pcmQueue.Dequeue();
                for (int i = n; i < data.Length; i++)
                    data[i] = 0f;
            }
        }
    }
}