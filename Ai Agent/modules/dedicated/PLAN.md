# Plan — Imasu Steamworks Dedicated Servers

Package `com.imasu.steamworks.dedicated` (optional addon). **Built (2026-08-30).**

## Status

Working: headless KCP server boot, Linux build helper, Control Center tab, smoke runner. Depends on **core only** (not LAN). No code until it was built this pass.

## Done

- `DedicatedHost` — auto-bootstraps when the build runs with `-imasuDedicated` (no scene wiring): loads the config from `Resources/Config/ImasuSteamworksConfig` (bundled by the build helper) and starts a server-only KCP session via `ImasuNetworkManager.StartDedicatedServer(port)` (new core method). No Steam session, no UI.
- `DedicatedBuildHelper` — `Imasu/Steamworks/Dedicated/Build Linux Headless Server…`: copies the Active Config into `Assets/Resources/Config` (Resources always ships) and builds a `StandaloneLinux64` player (now with **Server subtarget**, scenes gathered from the active Unity 6 Build Profile with classic-Build-Settings fallback).
- Control Center tab via `IImasuModuleEditor` (`ImasuDedicatedModule`) — info + build button.
- `--smoke=dedicated` runner (verifies `StartDedicatedServer` reaches `NetworkServer.active`).
- Runtime module contract `ImasuDedicatedAddon : IImasuModule` (2026-08-30).
- **CLI/env overrides (2026-08-30):** `--port`/`-port` (env `IMASU_PORT`), `--maxPlayers` (env `IMASU_MAX_PLAYERS`), `--map` (env `IMASU_MAP`) applied after config load — a server deploys with flags, no config rebake.
- `Dockerfile` stub + `docs/deploy.md` (2026-08-30) — working happy-path container; hardening is a research backlog item.

## Remaining

- **Smoke run pending a display/real machine** (the player can't init graphics in a headless shell).
- Server browser: advertise dedicated hosts via LAN discovery.
- Docker hardening (healthcheck, log rotation, resource limits).