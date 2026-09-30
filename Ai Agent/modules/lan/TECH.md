# Technical notes — Imasu Steamworks LAN

- **Discovery:** Mirror `NetworkDiscoveryBase` over UDP; `LanDiscovery` builds `LanServerInfo` and answers `LanDiscoveryQuery.Find()`.
- **Decoupling:** core references `ILanDiscovery`/`LanServerInfo`/`LanDiscoveryQuery`; `LanDiscovery` implements them. No core→addon asmdef reference.
- **Browser merge:** server browser merges Steam lobbies (filtered by a unique marker key so foreign Spacewar-appid lobbies never show) with LAN servers.
- **Defines:** `IMASU_LAN` in dev ProjectSettings + `versionDefines` on the core asmdef for consumers.