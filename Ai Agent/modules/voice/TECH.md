# Technical notes — Imasu Steamworks Voice

- **Codec:** `SteamUser.VoiceRecord` → compressed bytes → Mirror **unreliable** channel (`[Command]`/`[ClientRpc]`, `Channels.Unreliable`) → `SteamUser.DecompressVoice` → PCM → thread-safe queue → streamed `AudioClip` → per-player 3D `AudioSource` (spatialBlend=1, logarithmic rolloff to config `voipMaxDistance`). Own voice skipped.
- **Decoupling:** core references `IVoiceMember` (state: mic live/speaking/failed, owner, display, bytes, `SetPushToTalk`); `VoicePlayer` implements it. No core→addon asmdef reference.
- **Defines:** `VOICE` in dev ProjectSettings + `versionDefines` on the core asmdef for consumers. Builder/smoke runners guard with `#if VOICE`.