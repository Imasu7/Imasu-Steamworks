# Plan — Imasu Steamworks LAN

Package `com.imasu.steamworks.lan` (optional addon, `defineConstraints: ["IMASU_LAN"]`).

## Status

**Working.** UDP LAN discovery (Mirror `NetworkDiscoveryBase`): finds LAN hosts without Steam, so LAN play works with Steam off. LAN ≠ Dedicated (see `../dedicated/`).

## Done

- `LanDiscovery` auto-initializes via `[RuntimeInitializeOnLoadMethod]`.
- Core decoupling via `ILanDiscovery` + `LanServerInfo` + `LanDiscoveryQuery.Find()`.
- Server browser merges Steam lobbies + LAN servers (foreign Spacewar-appid lobbies filtered by a marker key).
- Non-Steam lobby flow (leave/start-match) fixed; `--smoke=lan-flow` green.

## Next

- Voice on LAN (blocked by design — voice uses Steam APIs; a non-Steam fallback is unplanned).
- Addon Manager integration (install/remove button + Control Center tab via `IImasuModule`).