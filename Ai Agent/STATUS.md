# Status

**Snapshot date:** 2026-09-30. `STATUS.md` is a snapshot, not a diary — the diary is
`CHANGELOG.md`.

## Repo state

This repo (`Imasu7/Imasu-Steamworks`) is the **canonical source** for the Imasu Steamworks
UPM packages. It was split out of the `Hidden Dev HDRP` game repo on 2026-09-30; the game repo
keeps embedded copies of the same packages under its own `Packages/` for development.

## Packages

| Package | Version | Role |
|---|---|---|
| `com.imasu.steamworks` | 0.1.3 | Core: lobbies, session manager, spawns, runtime uGUI screens, Control Center, Package Manager, config ScriptableObject |
| `com.imasu.steamworks.voice` | 0.1.3 | Steam VOIP capture/playback, voice HUD, distance-attenuated 3D playback, unreliable Mirror channel transport |
| `com.imasu.steamworks.lan` | 0.1.3 | LAN server discovery + KCP play, no Steam required |
| `com.imasu.steamworks.dedicated` | 0.1.3 | Headless dedicated-server host + Linux/headless build helper |
| `com.imasu.facepunch` | 0.1.1 | Facepunch.Steamworks DLLs + FizzyFacepunch transport (free MIT dep, not sold) |
| `com.mirrornetworking.mirror` | 96.11.0 | Vendored Mirror 96.11.0 (free MIT git-path shim, not sold) |

Scoped tags for all six exist on this repo, so consumer git URLs resolve.

## Functionally complete

- **Lobbies** — create/join, lobby settings stored as lobby data, lobby code display, invite
  overlay, Steam avatars, host migration on host-leave (`SteamLobbyManager`).
- **Listen-server session** — `ImasuNetworkManager` (host/client/dedicated), explicit
  transport selection (FizzyFacepunch for Steam, KCP for local), scene layout
  main menu → waiting room → match via `StartMatch` / `ServerChangeScene`.
- **Waiting room** — player list, ready state, chat (`LobbyChat`), invites, leave, host start.
- **Runtime uGUI screens** built in code by `ImasuSessionUI` (main menu, game setup, waiting
  room) plus `ImasuPauseMenu`, `ScoreboardUI`, `SettingsMenu` / `GameSettings`.
- **Spawns** — `SpawnPoint` / `SpawnArea` / `SpawnManager` with team filter, anti-clump and
  round-robin selection, wired into `OnServerAddPlayer`.
- **Editor tooling** — Control Center (Home, Project & Session, Gamemodes & Maps, Spawns &
  Teams, Advanced) and the Imasu Package Manager window (`ImasuPackageManager.cs`, backed by
  `catalog.json`, UPM add/remove by git URL).
- **Addons** — voice, LAN, dedicated all ship runtime `IImasuModule` implementations and
  compile against core with no reverse dependency. Dedicated ships the `-imasuDedicated`
  boot flag, config-from-Resources load, Linux headless build helper and Dockerfile.
- **Publish pipeline** — `release/<pkg>@<ver>` → validated scoped tag + `.tgz` + draft
  Release. Consumer install from git URLs compiled clean in a throwaway project.

## Not done

Open issues live per package in `Ai Agent/modules/<name>/ISSUES.md`. Summary:

**steamworks (core)**
- Cross-user Steam paths unverified: join-by-code → `StartClient` toward the lobby owner, and
  two-way voice relay / distance fade between two real users. Steam P2P cannot self-connect,
  so this needs a second account or machine.
- Player smokes not re-run in the latest pass: `--smoke=ui`, `--smoke=lan-flow`,
  `--smoke=dedicated`. The harness lives in the game repo and needs a machine with a display.
- Consumer install proven headlessly (git URLs for core + LAN + voice + the Mirror/Facepunch
  shims compiled with zero errors in a throwaway project); the GUI click-through of the
  Control Center bootstrap is still outstanding on a display machine.

**voice**
- Two-user relay (distance fade + push-to-talk between real users) unverified.
- Headless voice capture returned 0 frames in VM runs — confirm on real hardware.
- Voice requires Steam (capture uses Steam APIs), so LAN sessions have no voice by design.

**lan**
- No voice on LAN (Steam-only capture); non-Steam voice fallback unplanned.
- Discovery on multi-NIC machines untested (may see itself or the wrong subnet).

**dedicated**
- `--smoke=dedicated` pending: confirm `StartDedicatedServer` reaches `NetworkServer.active`
  on a real machine.
- Config is baked into `Resources` by the build helper — changing the Active Config afterwards
  needs a rebuild (CLI/env config overrides are planned for core).
- The Linux build uses the Build Settings scene list; keep it in sync or new maps are missing.

## Known rough edges

- Packages are still **developed in-tree** inside the `Hidden Dev HDRP` project, which keeps
  embedded copies at `Packages/`. A change only ships once it is pushed here and re-tagged.
- **No unit tests anywhere in this repo.** Verification is smoke/harness based and needs a
  machine with a display; the Steam paths need the Steam client logged in (App ID 480).
- `Packages/com.imasu.steamworks/Runtime/UI/ImasuSessionUI.cs` is a ~1993-line single file
  building every screen. It is the main refactor candidate.
- The game repo still has root-level `.csproj` files that reference the pre-split package
  layout. Noted only; out of scope for this repo.

## Known bugs

### B1 — LAN and Voice addon editor assemblies never compile

`Packages/com.imasu.steamworks.lan/Editor/Imasu.Steamworks.LAN.Editor.asmdef` carries
`defineConstraints: ["IMASU_LAN"]` and
`Packages/com.imasu.steamworks.voice/Editor/Imasu.Steamworks.Voice.Editor.asmdef` carries
`defineConstraints: ["VOICE"]`. Neither `IMASU_LAN` nor `VOICE` is defined in any
`ProjectSettings.asset` (checked the dev project on 2026-09-30), so both editor assemblies are
excluded from the build.

Consequence: `ImasuLanModule` and `ImasuVoiceModule` never compile, so
`ImasuPackageManager.DiscoverModuleEditors()` can never return an `IImasuModuleEditor` for
those two addons, and the Control Center **Modules tab silently shows no settings for LAN or
Voice**. The runtime halves of both addons are fine — their asmdefs carry no constraints, which
is the intended D20 "installed == active" model. Only the editor half is stale.

This is leftover from the D20 refactor, which removed the runtime guards and the
ProjectSettings defines but left the editor-side `defineConstraints` behind. Nothing caught it
because there are no unit tests and the omission is invisible in a player build.

Fix is one of two, and it is a product decision, not a mechanical one:

- **Remove the two `defineConstraints` lines** (matches D20 and the runtime asmdefs; the
  editor settings simply always compile). Preferred unless LAN/Voice editor settings must
  stay off unless Steam is configured.
- **Add `IMASU_LAN` and `VOICE` to the dev project's `ProjectSettings` and document that
  consumers must define them too.** Keeps the gate but means git-installed consumers get no
  addon editor UI unless they edit their own project settings — a bad default for an asset.

Either way this is a behaviour change, so per `RULES.md` it needs a package version bump and
a `CHANGELOG.md` entry.

### B2 — module docs still describe the pre-refactor define-gated addon model

`Ai Agent/modules/voice/{PLAN,DECISIONS}.md` and `Ai Agent/modules/lan/{PLAN,DECISIONS}.md`
still state the addon asmdefs use `defineConstraints: ["VOICE"]` / `["IMASU_LAN"]` and that the
defines live in the dev project's `ProjectSettings`. That was true before the D20 refactor and
is now wrong. `Ai Agent/modules/steamworks/TECH.md` likewise says addons "are guarded by
`defineConstraints`".

`Ai Agent/modules/lan/ISSUES.md` already records the removal correctly and
`Ai Agent/modules/steamworks/DECISIONS.md` D20 carries the "installed == active" reality, so
the module docs are internally inconsistent. Until they are reconciled, **trust the root
`Ai Agent/DECISIONS.md` D20 and `Ai Agent/PROJECT_HEALTH.md` over the module docs** on this
point.

Separately, `Ai Agent/modules/steamworks/PLAN.md` still uses the package's pre-rename name
`com.hidden.steam-multiplayer` in its layout tables (the real name is `com.imasu.steamworks`)
and points at an `Ai Agent/stacks/` folder that does not exist in this repo.

## Next

0. Decide and fix **B1** (dead LAN/Voice editor assemblies) — pick a lane, then version-bump and
   changelog it. B2 should be reconciled in the same pass.
1. Run the outstanding display-machine verification: `--smoke=ui`, `--smoke=lan-flow`,
   `--smoke=dedicated`, then the two-account Steam pass (join by code, host migration, voice
   fade, lobby chat). Record results in `Ai Agent/VERIFICATION.md`.
2. Do the Control Center bootstrap GUI click-through in a throwaway Unity 6 project.
3. Split `ImasuSessionUI.cs` into per-screen builders (main menu / setup / waiting room /
   scoreboard) behind the existing `ImasuSessionUI` entry point — behaviour-preserving.
4. Add CLI/env config overrides for the dedicated server so a baked `Resources` config is not
   the only source.
5. Re-vendor Mirror when a newer upstream tag is wanted (D4 — re-vendor, never hand-edit).
6. Publish a store-ready package cut once the above is green.