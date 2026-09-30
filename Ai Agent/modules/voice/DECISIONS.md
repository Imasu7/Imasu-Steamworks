# Decisions — Imasu Steamworks Voice

Locked choices for the `com.imasu.steamworks.voice` addon package (distance VOIP).

## D8 — Voice is Steam capture + Unity 3D audio

**Choice:** Use Steam Voice for record/compress/decompress. Play through a per-player 3D `AudioSource`. Distance volume is Unity's, not Steam's.

**Why:** Steam Voice has no proximity model. Putting the AudioSource on the remote player is the standard, simple way to get distance and direction. v1 is proximity-only (no team radio).

**Implementation (2026-08-22):** `VoicePlayer` (NetworkBehaviour) captures via `SteamUser.VoiceRecord` and sends compressed bytes over Mirror's **unreliable** channel (`Channels.Unreliable`), decompresses with `SteamUser.DecompressVoice` into a streamed `AudioClip` fed from a thread-safe PCM queue, played on a 3D `AudioSource` with logarithmic rolloff to `LobbySettings.voiceRange`. Own voice is skipped.

## Voice addon architecture (2026-08-24, from D20 in the root index)

**Choice:** `VoicePlayer`/`VoiceHud` ship as the optional package `com.imasu.steamworks.voice` (asmdef `Imasu.Steamworks.Voice`, `defineConstraints: ["VOICE"]`). Core compiles without it by depending on the core interface `IVoiceMember` (voice state: mic live/speaking/failed, owner, display, bytes, `SetPushToTalk`). Voice requires Steam by design (mic capture uses Steam APIs) — LAN sessions have no voice unless a non-Steam fallback is added later.

**Why:** Same interface-decoupled addon pattern as every other Imasu module, so core never hard-references an addon type and there are no asmdef cycles.