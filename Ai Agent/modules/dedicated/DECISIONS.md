# Decisions — Imasu Steamworks Dedicated Servers

Locked choices for the `com.imasu.steamworks.dedicated` addon package (planned).

## Dedicated is a separate addon, depends on core only (2026-08-24, from D19/D20 in the root index)

**Choice:** `com.imasu.steamworks.dedicated` will be a headless dedicated-server addon: headless host, build-profile helper, server logs. It depends on **core only** — not on LAN — and is **never bundled** with LAN. You pick the transport at runtime (KCP/Telepathy for a plain listen/headless server; Steam transport where a Steam identity exists).

**Why:** LAN ≠ Dedicated. A headless dedicated host is a different deployment story (Linux, no UI, no Steam client session necessarily) than same-network LAN play. Keeping the dependency core-only means the addon can be developed, versioned, and installed independently (PurrNet-Packages model).

## Status

Planned only — no code. Tracked in `ROADMAP.md` as a "later" slice. Do not start until the Control Center Addon Manager and `IImasuModule` registry exist (see `modules/steamworks/ROADMAP.md` §4).