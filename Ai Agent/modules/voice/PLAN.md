# Plan — Imasu Steamworks Voice

Package `com.imasu.steamworks.voice` (optional addon, `defineConstraints: ["VOICE"]`).

## Status

**Working.** Steam distance VOIP: capture → compress → unreliable-channel send → decompress → per-player 3D `AudioSource` with distance rolloff. Voice indicators, push-to-talk, invite/scoreboard mic-state, HUD. Requires Steam (D8).

## Done

- Steam voice codec path verified (record → compress → decompress).
- `VoicePlayer` (NetworkBehaviour) + `VoiceHud`; TX/RX activity tracking + owner Steam ID.
- Distance attenuation: OFF = Fixed (per-user VOIP volume), ON = Range (min/max rolloff) — host-owned.
- Waiting-room rows show mic-live/talking + sent/received KB.
- Core decoupling via `IVoiceMember`; UI degrades gracefully without the addon.
- Control Center tab (`IImasuModuleEditor`) + runtime module contract (`ImasuVoiceAddon : IImasuModule`, config-validated).
- VoiceSmokeRunner (`#if VOICE`); `--smoke=ui`/`lan-flow` green with voice guarded.

## Next

- Cross-user relay verification (two real users: distance fade, PTT).
- Non-Steam voice fallback (unplanned; LAN has no voice by design).