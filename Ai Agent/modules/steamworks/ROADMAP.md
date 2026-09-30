# Imasu Steamworks — Roadmap (Control Center + modular packages)

Status snapshot: **data model + Session Manager + spawns + Control Center tabs implemented; package split EXECUTED** (Voice + LAN separate UPM packages); **Control Center reworked into purpose tabs** (Project Scenes & Lobby, Gamemodes editor with draggable map list, Spawns & Teams workshop, debounced Detailed Config) with **perf caches (D24)** and additive Build Profile sync; **Scene whitelist validation done** (on-demand Validate Maps); **Addon Manager done** (`IImasuModuleEditor` registry + Install/Remove/Update via `Client.Add/Remove`, embedded detection); **`com.imasu.steamworks.dedicated` built** (DedicatedHost + Linux build helper + Control Center tab + smoke runner). Character controller is a separate module (`../character/`).

## Goal

A Gaia-style **master hub** (load a config, control every option, guided setup) plus
**PurrNet-Packages-style modules**: small UPM packages you install as needed, each
adding its own settings tab. **Core is the only default.** Voice, LAN and Dedicated
are separate optional addons.

## 1. Modular package layout (planned)

```
Packages/
  com.imasu.steamworks                     CORE  (this package today — Config, Steam lobbies,
                                             Session Manager prefab, spawns, Control Center shell)
  com.imasu.steamworks.voice               optional — distance VOIP (Steam voice codec)
  com.imasu.steamworks.lan                 optional — KCP/Telepathy LAN play, NO Steam (separate addon)
  com.imasu.steamworks.dedicated           later — headless dedicated-server hosting (separate from LAN)
  com.imasu.character                      separate module — first-person character controller (see ../character/)
  com.imasu.character.multiplayer          separate module — controller multiplayer bridge (steamworks-based)
  com.mirrornetworking.mirror              peer dependency (vendored embedded)
```

Rules:
- **Core is the only default** — no voice/LAN/fancy packages unless installed.
- Each optional package ships one `IImasuModule` so the Control Center lists it,
  shows `Installed` / `Not installed`, and exposes its settings tab when present.
- LAN and Dedicated are **two different addons**, never bundled. Dedicated depends
  on core only, not on LAN (you pick transport at runtime).

### `IImasuModule` registry (planned)

Core declares the interface; addons implement it and are discovered by reflection on
assembly reload:

```csharp
public interface IImasuModule
{
    string Id { get; }              // e.g. "com.imasu.steamworks.voice"
    string DisplayName { get; }
    bool IsInstalled { get; }       // package present?
    // Called when the Control Center draws this module's tab:
    void DrawSettings();            // addon draws its own EditorGUI section
    string InstallUrl { get; }      // git URL for the Addon Manager, or "" if built-in
}
```

Control Center behaviour: the Imasu Package Manager window lists every discovered `IImasuModule` with
Install/Remove/Update buttons (see the Addon Manager below) and opens a per-module
tab when `DrawSettings` is implemented. Core ships the registry + the built-in
"Core" entry only.

## 2. GitHub tracking / versioning

- **Monorepo** `Imasu7/Hidden-Dev-HDRP`, packages at `Packages/com.imasu.<module>/`.
- **Scoped tags** (OpenUPM convention): `com.imasu.steamworks@0.2.0`,
  `com.imasu.steamworks.voice@0.2.0`. `package.json:version` is the authority;
  the git tag pins the manifest.
- Consumer install: Window > Package Manager > Add by git URL
  `https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.2.0`
- One `.github/workflows/publish.yml`: validate package.json version bumps, tag, and
  draft a GitHub Release per package (with `.tgz`).
- Dev loop today keeps `file:` embedded packages (fast iteration); publish via `path:`.

### Addon Manager — GitHub install/remove from the Control Center (DONE 2026-08-30)

Implemented in the Imasu Package Manager window: `IImasuModuleEditor` registry + `catalog.json` + Install/Update/Remove via `Client.Add`/`Client.Remove` with confirm dialogs, tag-pinned git URLs, embedded-vs-remote detection. **Publish exercised live** — `release/<pkg>@0.1.0` tags for all four packages ran the workflow and produced scoped tags + `.tgz` Releases (see `TECH.md` §Publish). Remaining: Update-vs-installed semver check + a search/filter (see STATUS next items).

- **Mechanism:** Unity's UPM natively supports git URLs, so the Control Center can call
  `UnityEditor.PackageManager.Client.Add("https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/<id>#<tag>")` and `Client.Remove("<id>")` directly, with progress + error callbacks. No custom package server needed.
- **Version source of truth:** `package.json:version` + scoped tags (`com.imasu.steamworks@0.1.0`). The Addon Manager installs the latest tag (or a user-picked tag), pins it in `Packages/manifest.json`, and offers **Update** (re-resolve to the newest tag) and **Remove** (`Client.Remove`).
- **Addon catalog:** `Packages/com.imasu.steamworks/Editor/Addons/catalog.json` maps module id → display name → description → path → tag; the Imasu Package Manager window renders from it, merged with discovered `IImasuModule`s.
- **UX guards (non-negotiable):**
  - Confirm dialog before install/remove (removal removes the manifest entry).
  - **Embedded-vs-remote detection:** in this dev repo the addons are embedded `file:` packages — buttons must detect that and show "Embedded (built-in)" instead of Install/Remove so you can't break the dev project.
  - Tag-pinned installs only (never install `#master` silently).
  - Defines/versionDefines handle define gating automatically (D20).
- **Consumer path:** the same buttons work in a consumer project where the addon is remote (after Mirror is supplied — see `TECH.md`).

## 3. Done in this pass (implemented + verified)

- **Package split executed.** `Runtime/Voice/{VoicePlayer,VoiceHud}` → `Packages/com.imasu.steamworks.voice` (asmdef `Imasu.Steamworks.Voice`, `defineConstraints:["VOICE"]`). `Runtime/Network/LanDiscovery` → `Packages/com.imasu.steamworks.lan` (asmdef `Imasu.Steamworks.LAN`, `defineConstraints:["IMASU_LAN"]`). Each has its own `package.json` (deps: core + mirror) and is auto-discovered as an embedded package.
- **Core compiles without the addons** via core interfaces: `IVoiceMember` (voice state: mic live/speaking/failed, owner, display, bytes, SetPushToTalk) implemented by `VoicePlayer`; `ILanDiscovery` + `LanServerInfo` + `LanDiscoveryQuery.Find()` implemented by `LanDiscovery` (which now auto-initializes via `[RuntimeInitializeOnLoadMethod]`). Core UI (ScoreboardUI, ImasuSessionUI) queries these interfaces — no hard reference to the addon types, no asmdef cycles. Defines `VOICE;IMASU_LAN` are in ProjectSettings (this dev project) + `versionDefines` on the core asmdef (consumer portability).
- **Prefab/type migration:** `DummyPlayer`'s `VoicePlayer` + scene `VoiceHud` GOs are added by `ProductionSceneBuilder` under `#if VOICE`; `VoiceSmokeRunner` is `#if VOICE`-guarded. The `ImasuSessionManager` prefab + `Game/Warehouse` scenes were rebuilt.
- (earlier pass items listed in CHANGELOG: data model overrides, Session Manager, spawns, Control Center 2.0, wizard).

## 4. Next slices (B → A order, per user)

1. ~~**Addon Manager**~~ — **done (2026-08-30)**: `IImasuModuleEditor` registry (editor surface) + `IImasuModule` runtime contract (scaffold, for runtime validation) + Modules-tab Install/Remove/Update via `Client.Add`/`Client.Remove` (Unity 6 poll API), embedded-vs-remote detection, addon catalog array. Voice/LAN/Dedicated register settings via `IImasuModuleEditor`.
2. ~~**`com.imasu.steamworks.dedicated` addon**~~ — **done (2026-08-30)**: `DedicatedHost` (auto-boot on `-imasuDedicated`, config from Resources, server-only KCP via `ImasuNetworkManager.StartDedicatedServer`), `DedicatedBuildHelper` (Linux headless build + config→Resources), Control Center tab, `--smoke=dedicated`. Depends on core only. **Smoke run pending a display/real machine** (player can't init graphics in the headless shell).
3. **Scene whitelist hardening** — **done (2026-08-30)**: `Validate Maps` button (Scenes & Lobby + Gamemodes tabs) opens each catalog scene additively, checks for a Session Manager + spawn, caches results, flags failures. Remaining nicety: auto-run instead of on-demand button.
4. **Tutorial in Docusaurus** — separate tutorial button on Home opens the built docs site; offline static `Docs/` stays as fallback.
5. **Gaia-style polish** — profile load/save (Active Config + Duplicate covers this today), map list now uses `ReorderableList` (drag + built-in add/remove); gamemode-level ReorderableList + search are optional extras.
6. **`com.imasu.facepunch` wrapper** (deferred) — Facepunch DLLs stay vendored in core for now (see decision: not worth a separate package boundary).
7. **Publish** — `.github/workflows/publish.yml` added (validate package.json version vs tag → scoped tag + tgz + Release). Needs the repo's first real version bump + tag to exercise.

## 5. Borrowed ideas (credited)

From PurrNet Packages / inspectors: addon catalog + install URLs + installed-status
detection; foldouts with summaries; yellow missing-reference + one-click fixes;
`PlayerSpawner`-style `IProvideSpawnPoints`; `NetworkManagerInspector` header badge /
disabled-when-live patterns.
From Gaia (pattern): "window manager with full configs — load a config or control all
options from here." Our Control Center is that window.

## 6. Not borrowing

PurrNet's codegen/RPC/hash baking/visibility-rule/teams system is out of scope — we
keep Mirror's spawning + our simple team config for now.