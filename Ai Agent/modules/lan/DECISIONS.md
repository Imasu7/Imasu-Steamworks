# Decisions — Imasu Steamworks LAN

Locked choices for the `com.imasu.steamworks.lan` addon package (LAN play without Steam).

## LAN addon architecture (2026-08-24, from D20 in the root index)

**Choice:** `LanDiscovery` ships as the optional package `com.imasu.steamworks.lan` (asmdef `Imasu.Steamworks.LAN`, `defineConstraints: ["IMASU_LAN"]`). Core compiles without it by depending on the core interface `ILanDiscovery` + `LanServerInfo` + `LanDiscoveryQuery.Find()` (auto-initialized via `[RuntimeInitializeOnLoadMethod]`). Defines `VOICE;IMASU_LAN` live in the dev project's ProjectSettings + `versionDefines` on the core asmdef for consumers.

**Why:** LAN is a **separate addon from Dedicated** (never bundled). It only supplies discovery; the transport (KCP/Telepathy) is chosen at runtime. Keep core dependency-free.

## Non-Steam voice

**Choice:** LAN sessions have **no voice** for now — voice capture uses Steam APIs, and a non-Steam voice fallback is a separate, unplanned round (see D8 in `../voice/DECISIONS.md`).

**Why:** PTT hides with a "Voice requires Steam" note when Steam isn't initialized; shipping a working Steam-voice path first beats a half-baked LAN voice path.