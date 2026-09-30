# Overview — Imasu Steamworks (package monorepo)

**Last updated:** 2026-09-30

This is the repo-level overview. It describes the **product** (the packages), how they relate,
and how a consumer uses them. Per-package planning lives in `Ai Agent/modules/<name>/`.

## What this is

A **reusable Unity 6 multiplayer stack**: Steam lobbies, networking, runtime session UI, voice,
LAN play and dedicated-server hosting, shipped as six UPM packages.

For: Unity developers who want a working Steam session in their own game without writing
lobby/transport/session plumbing. It is game-agnostic — it knows nothing about any specific
game's scenes, roles, or rules.

## Layers

```
consumer game project (any Unity 6 project; e.g. the Hidden Dev HDRP game repo)
        |
        v
com.imasu.steamworks            <- core, always required
  config / master / lobbies / session manager / spawns / runtime UI / Control Center
        |
        +--> com.imasu.steamworks.voice       <- optional addon
        +--> com.imasu.steamworks.lan         <- optional addon
        +--> com.imasu.steamworks.dedicated   <- optional addon
        |
        v
com.imasu.facepunch            <- free MIT dep: Facepunch.Steamworks DLLs + FizzyFacepunch
com.mirrornetworking.mirror    <- free MIT dep: vendored Mirror 96.11.0
```

Netcode is **Mirror**; the Steam API is **Facepunch.Steamworks**; the production transport is
**FizzyFacepunch** (Mirror over Steam Networking Sockets / Relay); **KCP** is the local editor
test transport, because Steam P2P cannot connect a client to itself. Transport choice is
explicit (`UseLocalTransport`), never by component order.

## Packages

| Package | Version | Role | Depends on |
|---|---|---|---|
| `com.imasu.steamworks` | 0.1.3 | Config ScriptableObject, Steam lobbies + session manager, spawns, runtime uGUI screens, Control Center + Package Manager windows | `com.unity.inputsystem`, `com.unity.ugui` (Mirror + Facepunch supplied by the two free deps) |
| `com.imasu.steamworks.voice` | 0.1.3 | Steam VOIP capture/playback, voice HUD, 3D distance attenuation, compressed voice bytes over Mirror's unreliable channel | `com.imasu.steamworks` |
| `com.imasu.steamworks.lan` | 0.1.3 | UDP LAN server discovery + join-by-IP, KCP play without Steam | `com.imasu.steamworks` |
| `com.imasu.steamworks.dedicated` | 0.1.3 | Headless server host (`-imasuDedicated`), config from `Resources`, Linux headless build helper | `com.imasu.steamworks` |
| `com.imasu.facepunch` | 0.1.1 | Facepunch.Steamworks DLLs + FizzyFacepunch (`Mirror.FizzySteam`) | — |
| `com.mirrornetworking.mirror` | 96.11.0 | Vendored Mirror 96.11.0 (Examples/Hosting/Tests excluded) | — |

## Runtime flow: config → master → components

- One global `ImasuSteamworksConfig` ScriptableObject holds every tunable (Steam, transport,
  scenes, gamemodes, maps, spawns, voice).
- `ImasuSteamworksMaster` (on the `ImasuSessionManager` prefab) mirrors that asset and pushes
  values onto components via `ApplyToComponents()`. Only the first master survives, as a
  `DontDestroyOnLoad` singleton.
- Maps are scene names nested in `gameModes[] -> maps[]` — no per-map config files. Effective
  values resolve **map → gamemode → global** (`EffectiveMaxPlayers`, `EffectiveMinPlayers`,
  `EffectiveSecurity`, `EffectiveVoipMaxDistance`).
- `CatalogVersion` is a static int bumped in `ApplyToComponents`, so UI rebuilds once per
  catalog change instead of on every repaint.

## Screens and session lifecycle

Runtime uGUI is built in code by `ImasuSessionUI` (main menu, game setup, waiting room),
alongside `ImasuPauseMenu`, `ScoreboardUI` and `SettingsMenu`.

```
main menu (offline scene)
  ├─ create game  -> Steam lobby created -> ImasuNetworkManager.StartHost (FizzyFacepunch)
  │                 -> game setup (gamemode, map, player count, voice range)
  │                 -> waiting room -> host Start Match -> match scene + spawns + voice
  ├─ join game    -> find/join lobby by code, list or invite -> StartClient
  │                 -> waiting room -> host starts -> match scene
  └─ local host/client -> same flow over KCP (editor testing, no Steam)

host leaves  ->  host migration: a remaining player is promoted, the lobby survives
```

## Addon contract (D20)

Core defines the interfaces and knows nothing about addons: `IVoiceMember`,
`ILanDiscovery` (+ `LanServerInfo` / `LanDiscoveryQuery`), `IImasuModule`. Addons implement
them and are active because they are **installed** — there is no kill-switch define, and core
compiles with zero references to any addon. Runtime validation goes through
`ImasuSteamworksMaster.DiscoverModules()`; the editor surface is
`IImasuModuleEditor`, discovered via `TypeCache`.

`com.imasu.steamworks.lan` and `com.imasu.steamworks.dedicated` are separate addons — LAN play
is not the same thing as headless hosting, and they are never bundled together.

## Editor tooling

- **Control Center** (`Imasu -> Steamworks Control Center`): tabs **Home | Project & Session |
  Gamemodes & Maps | Spawns & Teams | Advanced**. Active config picker, gamemode/map catalog
  with per-map overrides, spawn/team editing, scene whitelist validation, build-settings sync
  into the active Unity 6 Build Profile, and an "Apply Active Config" that pushes to every
  Session Manager in the open scenes.
- **Imasu Package Manager** (`Imasu -> Package Manager`): install/update/remove every package
  plus the free Steam dependencies, driven by
  `Packages/com.imasu.steamworks/Editor/Addons/catalog.json` and
  `UnityEditor.PackageManager.Client.Add(gitUrl)` (poll-based — Unity 6 UPM has no completion
  event).

Both obey the D24 performance contract: no `OnGUI` frame scans the project, the scene, or the
package registry. See `Ai Agent/modules/steamworks/TECH.md`.

## Voice

Steam supplies compressed microphone audio with **no** proximity awareness. Unity supplies
distance: a 3D `AudioSource` at each remote player's head. Two modes — **fixed** (2D, per-user
gain, no dropoff) and **range** (3D log rolloff between min and max distance from the config).
Compressed voice bytes travel over Mirror's unreliable channel, not the reliable one.

## Publishing and consumption

Push `release/<pkg>@<version>`; `.github/workflows/publish.yml` validates the version, creates
the consumer-facing scoped tag `<pkg>@<version>`, packs a `.tgz`, and drafts a Release.

```json
{
  "dependencies": {
    "com.imasu.steamworks": "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.1.3",
    "com.mirrornetworking.mirror": "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0",
    "com.imasu.facepunch": "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.facepunch#com.imasu.facepunch@0.1.1"
  }
}
```

Addons install the same way with their own path and tag. Consumers can also use the Control
Center's "Install Steam dependencies" button, which performs the same adds. Note: Unity's
Package Manager does not support git dependencies declared inside a `package.json`, so the
Mirror and Facepunch entries are a bootstrap step for consumers, not a transitive resolve.

Consumer-facing documentation is offline HTML in `Docs/steamworks/`.

## Deliberately out of scope

- Game rules, roles, win conditions, scoring semantics tied to a game
- Matchmaking, ranking, progression, cosmetics
- Any backend, backend code or player-data storage
- Character controllers, weapons, animation, cameras
- Console or mobile platforms

This repo is transport, session and session-UI plumbing. Everything above it is the
consumer's.

## Files in this folder

| File | Purpose |
|---|---|
| `RULES.md` | Agent rules: changelog, version bumps, no game code, release procedure. |
| `STATUS.md` | Current snapshot: done, open issues, rough edges, next steps. |
| `OVERVIEW.md` | This file — product, package map, runtime flow. |
| `DECISIONS.md` | Repo-wide locked decisions. |
| `CHANGELOG.md` | Reverse-chronological history since the split. |
| `GLOSSARY.md` | Beginner definitions. |
| `PROJECT_HEALTH.md` | UPM constraints, dependency licensing, publish routine. |
| `VERIFICATION.md` | What to run by hand, and what needs a display machine. |
| `modules/<name>/` | Per-package PLAN, ROADMAP, DECISIONS, ISSUES, TECH. |