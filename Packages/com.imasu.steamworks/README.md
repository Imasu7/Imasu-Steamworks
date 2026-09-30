# Imasu Steamworks

Reusable **Steam multiplayer** for Unity 6 — lobbies, Mirror transport, distance voice, LAN fallback, and a single configuration window. Game-agnostic: add it to any project.

## Install

**In a new Unity 6 project** (via the Package Manager):

```
https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.steamworks#v0.1.0
```

Or open `Imasu → Steamworks Control Center` in the project that already has it and use the **Modules** tab — it installs/removes/updates the optional addons for you.

Requirements: Unity **6000.x**, Mirror (vendored automatically as a dependency), Steam client installed for online play.

## Quick start

1. **Open** `Imasu → Steamworks Control Center`.
2. **Create a config** (or duplicate the template), then set your **Steam App ID** (dev: 480 = Spacewar).
3. **Project Scenes & Lobby** — set `mainMenuScene` and `lobbyScene`, then **Sync Build Settings**.
4. **Gamemodes & Maps** — add a gamemode and its maps (a map is just a scene name). Add a scene and it appears everywhere: Host Setup, browser, in-match Change Map.
5. **Spawns & Teams** — add spawns to your map scenes and keep them on **Any (0)** until teams are assigned.
6. **Add the `ImasuSessionManager` prefab** to your scenes (button in the Control Center) and press Play.

Optional addons (install from the Modules tab):
- **Voice** (`com.imasu.steamworks.voice`) — distance VOIP via the Steam voice codec.
- **LAN** (`com.imasu.steamworks.lan`) — LAN play without Steam.
- **Dedicated Servers** (`com.imasu.steamworks.dedicated`) — headless server hosting.

## Docs

Offline docs live in `Docs/` (double-click `Docs/index.html`) — organized by module, no server needed.

## How it works

One **`ImasuSteamworksConfig`** ScriptableObject is the single source of truth. The `ImasuSessionManager` prefab (network manager, Steam, transports, lobby, spawns, UI on one object) reads it at runtime. Maps are scene names inside `gameModes[]` — no per-map configs.

## License

See `LICENSE` (proprietary — contact for reuse terms).