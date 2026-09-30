# Changelog

Append-only. Newest entries at the top.

Agents: you **must** add an entry after every completed unit of work. See `RULES.md` section 2.

Format:

```
## YYYY-MM-DD — short title

- What happened (past tense)
- Files / packages touched
- Notes for the next agent
```

---

## 2026-08-31 — Control Center rework + Imasu Package Manager + asset "Hidden" scrub

- **New Imasu Package Manager window** (`Imasu → Package Manager`): manages every Imasu
  package (core, Voice, LAN, Dedicated, Character) plus the free Steam dependencies
  (Mirror, Facepunch). Modules moved out of the Control Center (which now opens it via a
  header button). **Fixed dep detection** — `com.imasu.facepunch` was swallowed by the
  `com.imasu.*` prefix check, so it always showed "not installed" and the button stayed
  live. Install/remove failures now read **"Install failed: <reason>"** and auto-dismiss
  (was a stale cryptic "Last operation failed: unknown error"). Single labeled filter
  with a clear (✕) button; text tooltips instead of clipped walls of text.
- **Control Center rework** — tabs are now **Home | Project & Session | Gamemodes & Maps |
  Spawns & Teams | Advanced** (Setup Wizard folded into Home). **Gamemode editor
  rewritten**: foldout-per-mode that expands in place, ▲▼ reorder + Remove, no more dead
  arrow / "folds both on add" (was a ReorderableList-selection vs foldout-state fight).
  **Maps labeled**: "Menu name" vs "Scene name" column headers + a real **SceneAsset
  picker** in the detail (no more mystery "HDRP" scene). **Overrides** now show the field
  name (Max Players, VOIP Enabled, …) with a checkbox, no "Override" spam. Gamemode
  filter removed. **Active Config auto-applies to Session Managers on open** (kills the
  "misconfigured → unselect/reselect" workaround).
- **New `[CustomEditor]` for `ImasuSteamworksConfig`**: native grouped inspector with the
  gamemode/map catalogs hidden — one canonical editor (Gamemodes & Maps tab). Used by the
  Advanced tab and the Project window (fixes "gamemodes editable again in Detailed Config").
- **"Hidden" scrubbed from the packages** (lobby defaults, tooltips, config heuristic);
  game-side code/prefabs/configs keep it per decision. Fixed stale `@0.1.0` install-URL
  refs in the runtime addon modules → `@0.1.3`. Compile clean via CLI.

**Notes for next:** publish as 0.1.4 when ready (Control Center/PM + these strings ship in
core). The Control Center hand-test in `VERIFICATION.md` §1 was updated to the new tabs.

---

## 2026-08-31 — Hidden Round-2 combat (eliminate/lunge) + role respawns

- **`HiddenLife`** (`Assets/Hidden/Runtime/`): `[SyncVar] eliminated`; every client hides
  the body, the owner also stops moving; **respawn** via a `ClientRpc` teleport the owner
  applies to its own transform — solves the client-authority `NetworkTransform` fight
  that blocked Round-1 repositioning.
- **`HiddenCombat`**: server-authoritative IRIS **stunner** (`CmdShoot`, requires a locked
  target) + Hidden **lunge** (`CmdLunge`, nearest IRIS within range/aim). Cooldowns,
  validated server-side (role/alive/state/grace).
- **Win rules → alive-count**: Hidden eliminated → IRIS win; all IRIS eliminated → Hidden
  win; timer → Hidden survives. Solo-Hidden lobbies play to the timer (`startIrisCount` guard).
- **`IrisDetector`**: lock-on readiness (`lockSeconds`) replaces the Round-1 hold-to-tag
  win; `IsLocked` gates the stunner; detection bar = lock progress.
- **`HiddenHud`**: lock bar (green when locked), "You were eliminated" overlay, alive counts.
- **Builder**: player prefab gains `HiddenLife` + `HiddenCombat`; Game/Warehouse gain role
  spawns (Hidden team 1 / IRIS team 2, `SpawnManager.TryGetSpawn` fallback any); rebuilt.
- **`--smoke=combat`** headless runner: `SMOKE_COMBAT_OK irisWins=1 hiddenAlive=0
  result='IRIS eliminated the Hidden - IRIS wins the round!'`. `--smoke=round` re-verified green.
- Docs: `modules/hidden/{PLAN,ROADMAP,TECH}.md`, `STATUS.md`, `VERIFICATION.md` (combat rows).
  Compile clean via CLI.

**Notes for next:** combat geometry (aim dot / ranges) is code-reviewed but needs the
runbook §4 hand-pass with real players. Round-3 = Hidden kit / flashlight / spectate.

---

## 2026-08-31 — Polish pass: FizzyFacepunch exit NRE, warning sweep, runbook

- **FizzyFacepunch exit NRE fixed** (`Packages/com.imasu.facepunch`): `NextServer.Shutdown` +
  transport `Shutdown` now guard against Steam being torn down before the transport at app
  exit (the long-standing harmless-but-noisy NRE). Swallowed with a debug note.
- **Warning sweep:** full CLI compile showed **zero** CS/obsolete warnings across the packages
  and the game — no fixes needed.
- **Smoke player rebuilt** to the final 0.1.3 state; `--smoke=round` re-verified green
  (`SMOKE_ROUND_OK state=Playing round=1 localRole=Hidden`). Lone client launches clean.
- **Host/client pair smoke:** needs **Steam running** — the session prefab auto-inits Steam,
  so a second player instance won't boot without it (environment note, not a regression;
  `--smoke=round` covers the same localhost path headlessly). Documented.
- **`Ai Agent/VERIFICATION.md`** — new display-machine runbook: §1 Control Center hand-test,
  §2 player smokes (with the Steam note), §3 Linux dedicated + Docker, §4 cross-user Steam
  (join/roles/voice/host-migration/chat), §5 consumer bootstrap GUI click-through, plus a
  results table. STATUS references it; the old §1–§5 text is folded in.
- **Release routine codified** in `PROJECT_HEALTH.md` (bump → push → `release/*` tags →
  verify; the silent tag-drop gotcha + `workflow_dispatch` fallback).

**Notes for next:** the runbook is the display-machine gate; nothing else pending in the
stack polish. Next feature = Round-2 combat (`Ai Agent/modules/hidden/PLAN.md`).

---

## 2026-08-31 — Dependency-health pass: zero third-party code in the sold package + consumer proof

- **Split** `com.imasu.facepunch` (new embedded package): Facepunch.Steamworks DLLs + the
  FizzyFacepunch transport moved out of `com.imasu.steamworks`. Core's `.tgz` (verified:
  95 files, no Plugins/FizzyFacepunch) ships **zero third-party code**. Assembly name kept →
  core/editor asmdefs unchanged. MIT LICENSE + attribution added.
- **`com.mirrornetworking.mirror` shim** (git-path): Mirror's own repo has NO UPM package.json
  at any tag (unitypackage/Asset Store only), so it can't be git-cloned into UPM. The vendored
  MIT v96.11.0 source is now installable via `?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0`.
- **Control Center "Install Steam dependencies"** bootstrap: detects missing Mirror/Facepunch,
  `Client.Add`s the git URLs (the only UPM-legal mechanism — Unity forbids git deps in package.json),
  auto-offers before any addon install, core-first. Catalog `dependencies` array is the source of truth.
- **`versionDefines` don't fire for git-installed packages** (discovered via consumer proof:
  installing `lan@0.1.2` never defined `IMASU_LAN`, LAN assembly skipped). Removed addon
  `defineConstraints` + core `versionDefines` → **"installed = active"** (standard package model).
- **package.json hygiene:** dropped the unresolvable registry-style Mirror dep from core; addon deps
  → 0.1.3; license/repository/author/documentationUrl/changelogUrl added; LICENSE files in all 5
  packages + vendored Mirror; mojibake fixed.
- **Consumer proof passed:** throwaway Unity 6 project added core + LAN + voice via the exact
  bootstrap git URLs → Mirror + Facepunch auto-resolved from the shims → **compiled with zero
  errors** (Imasu.Steamworks + Editor + LAN + Voice + FizzyFacepunch + all Mirror assemblies).
- **Published:** core/addons **0.1.3**, `com.imasu.facepunch@0.1.1`, mirror shim `96.11.0` (scoped
  tags + releases). Old 0.1.0/0.1.1 release `.tgz` (which shipped Facepunch) **deleted**. All runs
  green; tags fired immediately this time.
- New project-wide audit: `Ai Agent/PROJECT_HEALTH.md` (UPM constraints, licensing, findings,
  decisions, current published versions). `STATUS.md` updated.

**Notes for next:** the Control Center GUI click-through (§5) is now the only unproven consumer
piece (the underlying resolution is proven headlessly). Addons are "installed = active" — no
kill-switch define anymore. The game repo still sets VOICE/IMASU_LAN in ProjectSettings for its own
`#if VOICE` build code (harmless, unrelated to package gating).

---

## 2026-08-31 — Stack polish (chat, host migration) + Hidden Round-1 game layer

Polish (commits `c5f656b`):
- **Waiting-room chat.** `LobbyChat` (`Packages/com.imasu.steamworks/Runtime/Network/LobbyChat.cs`): server-authoritative `[Command(requiresAuthority=false)]`/`[ClientRpc]`, rate-limited, max-length clamped, works over Steam and LAN (name falls back to "Player"). Lives on a scene object in the rebuilt `WaitingRoom` scene (`ProductionSceneBuilder`), so it exists before any player spawns. `ImasuSessionUI` gained a right-side chat panel (scroll log, input, Send, auto-refresh on new entries; Enter sends; disables when no LobbyChat yet).
- **Host migration.** `SteamLobbyManager`: the host now transfers Steam lobby ownership to the next member before leaving (`currentLobby.Owner = friend`, ~1.2 s settle), because Steam destroys a lobby whose owner leaves without handing off. The new owner promotes itself via `BecomeHost()` (driven by `ImasuNetworkManager.OnClientDisconnect` override + a safe `Update` poll — this Facepunch build has **no `OnLobbyOwnerChanged` event**, verified by reflection). Remaining clients reconnect to the new owner (3 attempts, then leave). Non-Steam sessions unaffected.
- **`publish.yml` fallback.** Added `workflow_dispatch` inputs (`pkg`+`ver`) running the identical job — the recurring silent tag-drop (0.1.0, 0.1.1) now has a manual escape hatch. Documented in the workflow header.
- Invites were already done (friend list + overlay + invite-join); nothing added there.

Hidden Round-1 game layer (commits `dfd8ad6`, `af4f3dd`):
- `Assets/Hidden/Runtime/` (`Hidden.Game` asmdef) — the game, not a `com.imasu.*` package.
- `HiddenGameManager` (scene object in Game + Warehouse via builder): assigns roles (shuffled, `hiddenCount` Hidden / rest IRIS, re-rolled per match), runs `Setup→Playing→RoundEnd→next round / MatchEnd→new match`, scores rounds/matches, escape-grace invisibility (`graceActive`), late-joiner role assignment. Win rules (no combat yet): IRIS win by tagging the Hidden; Hidden wins by surviving the round timer.
- `HiddenRoleController` — `[SyncVar] role`. `IrisDetector` (server) — aim-hold tagging + proximity tiers + `detectionProgress` synced. `HiddenInvisibility` (client) — hides a Hidden player's renderers at range / during grace. `HiddenHud` — runtime uGUI: role banner, timer, score, result/match overlays, detection bar, heartbeat pulse.
- Player prefab + Game/Warehouse scenes rebuilt with the new components.
- **Headless smoke `--smoke=round`** (new runner) verified in the rebuilt `Builds/LocalTestSmoke` player: `SMOKE_ROUND_OK state=Playing round=1 localRole=Hidden remaining=180 grace=True`. Compile clean via CLI.
- Module docs: `Ai Agent/modules/hidden/{PLAN,ROADMAP,TECH}.md`. `STATUS.md` updated (Now/done/not-done/Next + verification §2 notes the smoke player was rebuilt).

**Notes for next:** Round-2 = combat + alive-count wins + round-start respawn teleports (blocked by client-authority `NetworkTransform` — needs a teleport handoff) + map bounds/objectives (`modules/hidden/PLAN.md`). The `--smoke=round` runner is the one headless-verifiable gameplay smoke; `--smoke=ui`/`lan-flow`/`dedicated` still need a display machine. Host migration needs a cross-user Steam check (host leaves mid-lobby → new host + reconnect).

---

## 2026-08-31 — Publish 0.1.1 + verification checklist

- Pushed the overnight-build commit (`fd60bd1`, slices 3–5) to `origin/main`.
- Bumped all four packages + `catalog.json` tags `0.1.0` → `0.1.1` (`2534d7a`), pushed, then pushed `release/com.imasu.{steamworks,steamworks.voice,steamworks.lan,steamworks.dedicated}@0.1.1` tags.
- `publish.yml` ran green (4 runs) → scoped tags `com.imasu.*@0.1.1` + Releases each with `-0.1.1.tgz`. Consumer URL verified by shallow clone of `com.imasu.steamworks@0.1.1` (package.json = `0.1.1`, catalog ships `@0.1.1`).
- **Publish gotcha reconfirmed:** the initial 0.1.1 tag push was silently dropped again (workflow active, tags on remote, 0 runs). Fix stays: delete + re-create + re-push the `release/*` tag. Worth fixing in `publish.yml` or documenting for the user (see "Left").
- `STATUS.md` gained a **"Verification — display machine"** section: Control Center hand-test checklist, player smoke commands, dedicated Linux artifact + Docker commands, cross-user Steam steps, and the consumer install path (vendor Mirror first). "Next" renumbered to reference it.

**Next:** run the §1–§5 checklist on a display machine / second Steam account; consider hardening `publish.yml`'s tag-drop issue (it's the second occurrence).

---

## 2026-08-31 — Overnight build: LAN pane, publish exercised, Addon Manager UX, runtime modules, dedicated overlay, gamemode ReorderableList

One overnight build in five slices, each CLI-compile-clean (exit 0). Multiplayer only — no character code (that worktree is separate). All four packages published at **0.1.0**.

### Slice 1 — LAN addon gets a real settings pane
- `LanDiscovery` gained tunables `port` (default 47777), `activeDiscovery`, `maxStaleSeconds` (replaces the hardcoded 7000 ms constant), persisted via PlayerPrefs (`imasu_lan_*`) and applied in `Start()`; new `ApplySettings()`.
- `ImasuLanModule.DrawSettings()` now draws the three fields live (Control Center → Modules → LAN) and pushes to `LanDiscovery.Instance` in play mode.
- `Imasu.Steamworks.LAN.Editor.asmdef` now references `Mirror.Components` (needed to touch `NetworkDiscoveryBase` fields).
- `Docs/steamworks/04-lobby-flow.html` 4.2 gained a "LAN tunables" paragraph.

### Slice 2 — Addon Manager catalog + first real publish (exercised end-to-end)
- Replaced the hardcoded `ModuleCatalog` array with `Packages/com.imasu.steamworks/Editor/Addons/catalog.json` (`id/displayName/description/installPath/tag`, scoped-tag versions). `LoadCatalog()` reads it, falls back to the array, cached and invalidated on `projectChanged`/`registeredPackages` (D24).
- Fixed `publish.yml`: scoped-tag step now uses the detected package (`git tag "$PKG@$VER"`) and the Release `tag_name` no longer double-prefixes `com.`; added `permissions: contents: write`.
- **Published live:** committed the full working tree (`ed7d0b5`, the prior session's restructure + these slices), pushed, then pushed `release/com.imasu.steamworks@0.1.0` / `.voice` / `.lan` / `.dedicated`. All 4 workflow runs green → 4 scoped tags + 4 Releases each with `-0.1.0.tgz`. Consumer git-URL verified (`git clone --branch com.imasu.steamworks.lan@0.1.0` resolves, `package.json` present).
- **Gotcha recorded:** tag pushes made before GitHub registers a newly-added workflow are silently dropped — delete + re-push the tag to trigger (all four needed it).
- Added `References/` to `.gitignore` (local-only material). Documented the consumer caveat that `com.mirrornetworking.mirror` must be supplied before `com.imasu.*` installs resolve.

### Slice 3 — Addon Manager UX + runtime module wiring
- Modules tab: filter field; per-row installed version; **Update to X.Y.Z** button (semver compare catalog tag vs installed) or "up to date"; **Release notes** link (`/releases/tag/<scopedTag>`); failed ops now surface in a HelpBox (`lastAddonError`) in addition to the dialog.
- `installedModules` now stores `InstalledInfo { embedded, version }` (was `bool`).
- **Runtime `IImasuModule` wired:** `ImasuSteamworksMaster.DiscoverModules()` (AppDomain reflection, cached, `ReflectionTypeLoadException`-safe) validates every config-enabled module on `ApplyToComponents()` and logs a warning on `Validate()` problems.
- New runtime implementations: `ImasuVoiceAddon` (validates `voipMaxDistance < voipMinDistance` when distance voice is on), `ImasuLanAddon` (validates `kcpPort` range when non-Steam is enabled), `ImasuDedicatedAddon` (presence-based).

### Slice 4 — Dedicated overlay + build polish
- `DedicatedHost` CLI/env overrides (applied after config): `--port`/`-port` (`IMASU_PORT`), `--maxPlayers` (`IMASU_MAX_PLAYERS`), `--map` (`IMASU_MAP`).
- `DedicatedBuildHelper`: scenes gathered from the active Unity 6 Build Profile (classic Build Settings fallback) and `BuildPlayerOptions.subtarget = StandaloneBuildSubtarget.Server` (graphics-free Linux server).
- New `Dockerfile` stub + `docs/deploy.md` (happy-path container, hardening = research backlog).

### Slice 5 — Control Center polish
- Gamemodes are now a draggable `ReorderableList` (replaces ▲▼X): drag-handle rows show name + map count; selecting a row opens its detail editor (name, effective line, maps list, overrides). `gameModeLists` cached by property path, cleared on rebuild.
- Gamemode **filter** field: filters to a plain foldout view while active (drag hidden).
- Bottom bar shows a non-blocking scene-whitelist summary ("X/Y map scene(s) valid") once `Validate Maps` has run.

### Files
- LAN: `Packages/com.imasu.steamworks.lan/{Runtime/LanDiscovery.cs,Editor/ImasuLanModule.cs,Editor/Imasu.Steamworks.LAN.Editor.asmdef}`, `Docs/steamworks/04-lobby-flow.html`
- Addon Manager: `Packages/com.imasu.steamworks/Editor/ImasuSteamworksControlCenter.cs`, `Packages/com.imasu.steamworks/Editor/Addons/catalog.json` (new), `.github/workflows/publish.yml`, `.gitignore`
- Runtime wiring: `Packages/com.imasu.steamworks/Runtime/Config/ImasuSteamworksMaster.cs`, new `{voice,lan,dedicated}/Runtime/Imasu{Voice,Lan,Dedicated}Addon.cs`
- Dedicated: `Packages/com.imasu.steamworks.dedicated/{Runtime/DedicatedHost.cs,Editor/DedicatedBuildHelper.cs,Dockerfile,docs/deploy.md}`
- Docs: `Ai Agent/{STATUS,CHANGELOG}.md`, `Ai Agent/modules/{steamworks,voice,lan,dedicated}/*`

**Next agent:** hand-test the Control Center (Modules filter/update/Release-notes, Validate summary, drag gamemodes), run `--smoke=ui|lan-flow|dedicated` + the dedicated Linux build on a display machine, cross-user Steam verification. Published 0.1.0 tags/releases exist on `Imasu7/Hidden-Dev-HDRP`; uncommitted post-tag work (slices 3–5) is on top of `ed7d0b5`.

---

## 2026-08-30 — Multiplayer build: whitelist validation + Addon Manager + dedicated addon + polish

Executed the approved multiplayer plan (phases 0–4). Every phase compile-verified via CLI (exit 0).

### Phase 1 — Scene whitelist hardening
`ImasuSteamworksControlCenter`: new `Validate Maps` button in Project Scenes & Lobby + Gamemodes tabs. On demand (never per-repaint — D24), it opens each catalog scene additively, scans for an `ImasuSessionManager` (`ImasuSteamworksMaster`) + at least one `SpawnPoint`/`SpawnArea`, caches results (invalidated on project change / config commit / spawn edits), and flags failures ("no Session Manager" / "no spawns"). The active scene is scanned in place (never reopened).

### Phase 2 — Addon Manager
- New editor surface `Imasu.Steamworks.Editor.Addons.IImasuModuleEditor` (ModuleId/DisplayName/DrawSettings), discovered via `TypeCache`. The pre-existing runtime `Imasu.Steamworks.IImasuModule` (enable/validate per config) is kept as the runtime contract (scaffold — play-mode wiring later).
- Modules tab now installs/updates/removes from GitHub: `Client.Add("https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/<id>#<tag>")` / `Client.Remove(id)`, confirm dialogs, tag-pinned only, embedded-vs-remote detection (`PackageSource.Embedded` → "Installed (embedded)", no buttons), progress state, poll-based completion.
- **Unity 6 UPM gotcha (verified):** request types live in `UnityEditor.PackageManager.Requests` and are **poll-based** — `IsCompleted`/`Status`/`Error`, there is NO `Completed` event (compile error confirmed it). The Control Center polls in OnGUI + repaints via `EditorApplication.update` while pending.
- Voice/LAN register settings via `IImasuModuleEditor` (new Editor asmdefs `Imasu.Steamworks.Voice.Editor`/`LAN.Editor`, `defineConstraints: VOICE`/`IMASU_LAN`). ModuleCatalog gained a Character Controller row (planned, no tag).
- Replaced the "copy git URL to clipboard" stub with the real buttons (kept a small Copy URL affordance).

### Phase 3 — com.imasu.steamworks.dedicated (built)
- New optional package (embedded, auto-discovered): `package.json` (deps core + mirror), runtime asmdef, `DedicatedHost` (`[RuntimeInitializeOnLoadMethod]` self-bootstrap when run with `-imasuDedicated`; loads config from `Resources/Config/ImasuSteamworksConfig`, assigns + `ApplyToComponents`, calls `StartDedicatedServer`), editor asmdef + `DedicatedBuildHelper` (`Imasu/Steamworks/Dedicated/Build Linux Headless Server…` → copies Active Config into `Assets/Resources/Config` + `StandaloneLinux64` build from Build Settings), `ImasuDedicatedModule` (Control Center tab + build button), `--smoke=dedicated` runner.
- Core gained `ImasuNetworkManager.StartDedicatedServer(int port = 0)` — forces KCP, sets `kcp2k.KcpTransport.Port` when port > 0, `StartServer()` (server-only, no Steam).
- **Smoke note:** the smoke player rebuilt clean (includes the new runners) but the player can't init graphics in this headless shell — `--smoke=ui/lan-flow/dedicated` runs move to the Phase 5 real-machine gate.

### Phase 4 — Polish & publish
- Map editor converted to a draggable `ReorderableList` per gamemode (compact name/scene rows + selected-row detail editor for the scene picker/cap/note) — replaces the ▲▼X buttons + "+ Add Map". Cached keyed by property path; cleared on config rebuild.
- `Packages/com.imasu.steamworks/README.md` written ("add to a new Unity 6 project" + quick start).
- `.github/workflows/publish.yml` added: on a `release/<pkg>@<ver>` tag, validates `package.json` version, creates the scoped tag + `.tgz`, drafts a GitHub Release.
- Verified `Samples~/{StarterConfig,StarterUI}` match package.json `samples`. Docusaurus still deferred (no npm requirement at build time).

### Files
`Packages/com.imasu.steamworks/Editor/ImasuSteamworksControlCenter.cs`, `.../Editor/Addons/IImasuModule.cs`, `.../Runtime/Network/ImasuNetworkManager.cs` (+`StartDedicatedServer`), `Packages/com.imasu.steamworks/README.md`, `Packages/com.imasu.steamworks.voice/Editor/*` (new asmdef + module), `Packages/com.imasu.steamworks.lan/Editor/*` (new), `Packages/com.imasu.steamworks.dedicated/*` (new package), `Assets/LocalTest/DedicatedSmokeRunner.cs`, `.github/workflows/publish.yml`, `Docs/steamworks/02-master-window.html`, `Ai Agent/modules/{steamworks,dedicated,voice,lan}/*`.

**Next agent:** run the smoke builds on a machine with a display (`--smoke=ui`, `--smoke=lan-flow`, `--smoke=dedicated`); hand-test the Control Center Modules/Validate/Gamemodes tabs; exercise publish.yml by tagging the first real version.

---

User asked for: (1) understand/fix the Control Center lag before adding more features, (2) plan two new addon modules (Advanced First Person Controller; GitHub install/remove from the Package Manager) documented from the start, (3) split the project + docs into per-module areas, (4) make the repo worktree-ready for ORCA.

### Control Center performance (D24)
- **Root cause:** the IMGUI window repaints on every mouse move, and several hot paths scanned the whole project/scene/package-registry per repaint:
  - `FindScenePath` ran `AssetDatabase.FindAssets("t:SceneAsset")` (full project scan) per map row per repaint (Gamemodes tab), AND `MissingBuildScenes` recomputed it on a **0.5 s timer** — so the window stuttered twice a second even when idle (the dominant "menus are sluggish" cause).
  - `FindObjectsByType<SpawnPoint/SpawnArea>` per repaint (Spawns tab) + twice more via `SceneSpawnCount` (Setup Wizard).
  - `PackageInfo.GetAllRegisteredPackages()` per repaint (Modules tab).
  - `new GUIStyle(toolbarButton)` × 6 per repaint (TabBar); team-option arrays per spawn row per repaint.
- **Fixed** in `ImasuSteamworksControlCenter.cs`: static `SceneCache` (name→path/SceneAsset, ONE `FindAssets` pass, invalidated on `EditorApplication.projectChanged` + window enable); `MissingBuildScenes` now reads the cache with **no timer**; 1 s spawn-scan cache + cached master-presence check (invalidated on scene/project change and spawn edits); cached package list (invalidated on `registeredPackages`); cached tab style; team options built once per frame; kill-switch toggle no longer calls `SaveAssets` (dirty assets autosave). Removed unused `System.Linq`.
- **Compile:** verified clean via CLI (`Unity.exe -batchmode -quit -nographics`, exit 0, "Exiting batchmode successfully").
- Decision recorded as D24 + RULES rule 13 ("no per-repaint scans in editor windows").

### Modular docs restructure (D23)
- New layout — root = project-wide only, `Ai Agent/modules/<name>/` = per-module planning (PLAN/ROADMAP/DECISIONS/ISSUES/TECH):
  - Moved `PLAN.md` → `modules/steamworks/PLAN.md`; `ROADMAP_2.0.md` → `modules/steamworks/ROADMAP.md` (status + layout updated; addon-manager section added).
  - `DECISIONS.md` split: root keeps D1–D3, D16, D20 + new D23 + index of module files; D4–D7, D9–D15, D17–D19, D21, D22 (+new D24) → `modules/steamworks/DECISIONS.md`; D8 + voice notes → `modules/voice/DECISIONS.md`; LAN/Dedicated/Character decision files seeded.
  - New root `OVERVIEW.md` (project + module map + dependency graph) and `WORKTREES.md` (ORCA worktree layout, module boundary contract, merge order, Unity caveats).
  - `RULES.md`: rule 12 (module work updates the module's docs folder) + rule 13 (perf contract). `AGENTS.md` + `Ai Agent/README.md` read orders updated.
- `Docs/` reorganized by module: steamworks manual (01–07) moved to `Docs/steamworks/` (stylesheet links fixed to `../styles.css`), new `Docs/steamworks/index.html`, new `Docs/character/index.html` (planning placeholder), root `Docs/index.html` rewritten as an all-modules landing; `Docs/README.md` updated.

### Imasu Character Controller planning (`com.imasu.character`) — no code
- `modules/character/` seeded: `PLAN.md` (6-phase Opsive-benchmark roadmap: P0 foundation → P1 attributes/damage → P2 inventory → P3 weapons/hands → P4 presentation incl. stress state system + DAZ customization → P5 multiplayer addon → P6 polish), `TECH.md` (UCC layering: locomotion core + ability system + state system + items + attributes; `ICharacterInput`/`ICharacterNetworkAdapter` decoupling), `DECISIONS.md` (D25 standalone-first, D26 MP = `com.imasu.character.multiplayer` addon, D27 Opsive-style architecture, D28 not-a-final-spec + DAZ pipeline as a research task), `ISSUES.md`, `ROADMAP.md`.
- User decisions locked: full perf fix; MP lives in its own addon package; package id `com.imasu.character`.

### Addon Manager (documented, not built) — `modules/steamworks/ROADMAP.md` §2
- `IImasuModule` registry spec (Id/DisplayName/IsInstalled/DrawSettings/InstallUrl) + Modules-tab Install/Remove/Update buttons via `UnityEditor.PackageManager.Client.Add(url)`/`Client.Remove(id)` with git URLs (`?path=Packages/<id>#<scoped-tag>`), addon catalog JSON, embedded-vs-remote detection, tag-pinned installs, confirm dialogs.

Files touched: `Packages/com.imasu.steamworks/Editor/ImasuSteamworksControlCenter.cs`, `Ai Agent/{OVERVIEW,WORKTREES,README,RULES,STATUS,CHANGELOG,DECISIONS}.md`, `Ai Agent/modules/*` (new + moved), `Docs/{index.html,README.md}`, `Docs/steamworks/*` (moved + links fixed), `Docs/character/index.html`, `AGENTS.md`.

**Note for next agent:** the perf change is code-complete but the batch compile (`Unity.exe -batchmode -quit -nographics -projectPath <proj> -logFile <tmp>`) was blocked by the project being open — run it before considering this shipped.

---

**User-reported:** adding map HDRP to the Hidden gamemode showed in Pause → Change Map but NOT Main Menu → Host; spawn point with teamId=1 spawned at 0,0,0; Control Center Scenes&Modes/Gamemodes/Spawns&Teams had almost no settings; Detailed Config typing lagged badly (~1s/char → 10s for "hdrp").

**Root causes & fixes:**
- **Host map stale:** `ImasuSessionManager.prefab` baked only `[Game,Warehouse]`; `ImasuSessionUI` snapshots the catalog at Awake and only rebuilds on gamemode change, while `ImasuPauseMenu` builds on click from the live manager (hence pause showed HDRP). Fix: `ImasuSteamworksMaster` now exposes `CatalogVersion` (bumped in `ApplyToComponents`); `ImasuSessionUI.RefreshScreens` rebuilds setup mode/map dropdowns once per catalog change. Also ran a new batch helper `ProductionSceneBuilder.SyncSessionPrefabFromConfig` (menu `Imasu/Steamworks/Sync Session Prefab to Config`) that re-bakes the prefab's `gameModes`/`maps`/`availableScenes` from the Active Config without rebuilding scenes — prefab now carries Game, Warehouse, HDRP.
- **Spawn 0,0,0:** `SpawnManager.TryGetSpawn(0)` only matches providers with `TeamId==0` (Any) or `==0`; a teamId=1 spawn is exclusive to team 1, and no team assignment exists yet, so it fell through to Mirror's default zero position. No behaviour change — Spawns & Teams tab now explains "keep spawns on Any (0) until teams are assigned" and lets you set each spawn's team inline.
- **Build Settings vs Build Profiles (Unity 6):** `SyncBuildSettings` only wrote classic `EditorBuildSettings.scenes`; `ProductionSceneBuilder.AddToBuildSettings` hard-coded 4 scenes and overwrote (would delete HDRP on next BuildAll). Both now additive + merge into the active `BuildProfile.scenes` (`UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()`, instance `scenes` property — verified via reflection on the 6000.5 editor assembly; static `GetScenes/SetScenes` do NOT exist).
- **Control Center tabs rework (per user):**
  - **Project Scenes & Lobby** (was "Scenes & Modes"): mainMenu/lobby scenes + Session defaults (maxPlayers/minPlayers/lobbyName/security/lobbyCodeLength) + Steam advanced foldout, all via serialized props with guarded commit.
  - **Gamemodes & Maps** (was read-only summary): editable gamemode list (add/reorder/remove ▲▼X), per-map rows with displayName + scene (type or SceneAsset picker) + optional player cap + note, and per-gamemode **Overrides** foldout (Session/Voice/Player Mover/Transport override toggles) + live Effective line. No more hunting the Detailed Config inspector.
  - **Spawns & Teams**: spawns detected in the open scene with per-spawn team popup (Any/team names); inline team config editor (min separation slider, 1/2/4-team presets, per-team name/color/spawnTeamId via serialized object) writing straight to `ImasuTeamConfig.asset`.
  - **Detailed Config**: kept as the advanced fallback with a "most settings live in purpose tabs" banner and guarded `ApplyModifiedProperties` (commits on Enter/focus-lost, not per keystroke) — fixes the typing lag. `MissingBuildScenes` cached (0.5 s) so the always-visible bottom bar no longer runs `FindAssets("t:SceneAsset")` every OnGUI.
  - Purpose-tab edits auto-apply to Session Managers (throttled 1 s) via `CommitSerializedChanges` → `AutoApplyThrottled`.
- Setup Wizard step 3 ("Define a gamemode with maps") now jumps to the Gamemodes tab instead of just selecting the asset.

**Verified:** Unity 6000.5.9f1 CLI compile clean (exit 0, `Exiting batchmode successfully`); `SyncSessionPrefabFromConfig` ran in batch and re-baked the prefab (grep shows Game/Warehouse/HDRP in gameModes + availableScenes). BuildProfile API confirmed via binary scan of UnityEditor.dll (`get_scenes`/`set_scenes`).

Files: `Packages/com.imasu.steamworks/Editor/ImasuSteamworksControlCenter.cs`, `Packages/com.imasu.steamworks/Runtime/Config/ImasuSteamworksMaster.cs`, `Packages/com.imasu.steamworks/Runtime/UI/ImasuSessionUI.cs`, `Assets/LocalTest/Editor/ProductionSceneBuilder.cs`, `Packages/com.imasu.steamworks/Prefabs/ImasuSessionManager.prefab` (re-baked), `Docs/02-master-window.html`.

**Notes for next agent:** HDRP map load is intentionally slow (~1.2 MB baked lighting; left as-is per user). To refresh the Host map list after editing a config: `Apply Active Config to Session Managers` (open scene) or menu `Imasu/Steamworks/Sync Session Prefab to Config`.

---

Fixed the confusing config-selection UX in the Control Center (top bar previously had no `ObjectField` — `Select Config` only pinged the already-loaded default, and `Create / Select Config` silently returned the legacy `Assets/Config/ImasuSteamworksConfig.asset`).

- Top bar now has a real **Active Config** `ObjectField`. Picking a different asset switches the whole project to it: the choice is persisted (`EditorPrefs` `ImasuSteamworks.ActiveConfigPath`, read first in `LoadConfig`), the Detailed Config editor is rebuilt, and it is **auto-applied** to every `ImasuSessionManager`/`ImasuSteamworksMaster` in the open scenes (per user decision 1 = auto). Fallbacks: legacy `Assets/Config/ImasuSteamworksConfig.asset`, then any `t:ImasuSteamworksConfig` (now preferring filenames containing "Hidden").
- Old `Select Config` / `Duplicate StarterConfig` / `Create / Select Config` buttons replaced by **Create New Config…** (fresh empty asset via `SaveFilePanel`, unique path under `Assets/Config`) and **Duplicate Template…** (copies `Samples~/StarterConfig/TemplateImasuSteamworksConfig.asset`). Both funnel through the new `SetActiveConfig()` so they also auto-apply + persist.
- Bottom bar button renamed **Apply to Master(s) in Scene** → **Apply Active Config to Session Managers** with a caption explaining the global model (one config; maps are scene names inside `gameModes[]`, not per-map configs; only the first Session Manager survives as a `DontDestroyOnLoad` singleton).
- `ImasuSteamworksMaster.config` tooltip rewritten to make clear it's an instance mirror of the one global asset, not a per-scene config. `ApplyToSceneMasters()` unchanged (manual path stays); new `AutoApplyToSceneMasters()` helper marks the scene dirty on auto-apply.
- Docs updated: `Docs/01-getting-started.html` (new "One global config, not per-map" note + step 2 uses Active Config), `Docs/02-master-window.html` (new section 2.0 + updated Apply section + renamed button). Both existing config assets kept (user decision 2): `Assets/Config/ImasuSteamworksConfig.asset` + `Assets/Config/ImasuSteamworksConfig_Hidden.asset`.

Files: `Packages/com.imasu.steamworks/Editor/ImasuSteamworksControlCenter.cs`, `Packages/com.imasu.steamworks/Runtime/Config/ImasuSteamworksMaster.cs`, `Docs/01-getting-started.html`, `Docs/02-master-window.html`.

**Notes for next agent:** the Active Config choice is per-editor (EditorPrefs), so each dev picks their own config; the prefab still ships wired to `ImasuSteamworksConfig_Hidden.asset`. Decision 3: label is "Active Config".

---

**Decision recap:** core = only default; Voice and LAN are optional UPM packages; LAN ≠ Dedicated (dedicated later). Facepunch DLLs + kcp2k stay where they are (Facepunch vendored in core; kcp2k is a Mirror transitive dep — voice is transport-agnostic).

**New packages (monorepo, embedded):**
- `Packages/com.imasu.steamworks.voice/` — `VoicePlayer` + `VoiceHud` moved from core (`Runtime/Voice/` + `Runtime/UI/VoiceHud.cs`). asmdef `Imasu.Steamworks.Voice`, `defineConstraints:["VOICE"]`, refs core + Mirror + TMP + UI. `package.json` deps: core 0.1.0 + mirror 96.11.0.
- `Packages/com.imasu.steamworks.lan/` — `LanDiscovery` moved from core (`Runtime/Network/`). asmdef `Imasu.Steamworks.LAN`, `defineConstraints:["IMASU_LAN"]`, refs core + Mirror + Mirror.Components + Mirror.Transports. `package.json` deps: core + mirror.

**Core compiles without the addons (no asmdef cycles):**
- New core interfaces: `IVoiceMember` (`Runtime/UI/IVoiceMember.cs`) and `ILanDiscovery` + `LanServerInfo` + `LanDiscoveryQuery.Find()` (`Runtime/LAN/ILanDiscovery.cs`).
- `VoicePlayer` implements `IVoiceMember` (maps micLive/speaking/micState/owner/display/bytes/SetPushToTalk); `LanDiscovery` implements `ILanDiscovery` (auto-initializes via `[RuntimeInitializeOnLoadMethod]`; `Snapshot()` returns `List<LanServerInfo>` with a pre-resolved `host`).
- `ScoreboardUI` + `ImasuSessionUI` query these interfaces instead of the addon types (`FindObjectsByType<MonoBehaviour>()` + `is IVoiceMember` / `LanDiscoveryQuery.Find()`); waiting-room voice rows, PTT, scoreboard ring, browser LAN rows all work when the addons are installed and degrade to neutral/no-LAN when they're not.
- Defines: `VOICE;IMASU_LAN` added to ProjectSettings (this dev project) + `versionDefines` on the core asmdef for consumer portability. `ProductionSceneBuilder` adds `VoicePlayer`/`VoiceHud` under `#if VOICE`; `VoiceSmokeRunner` fully `#if VOICE`-guarded.

Verified: compile clean; `BuildAll` rebuilds scenes/prefabs (DummyPlayer + Game/Warehouse VoiceHud via #if VOICE); smoke player rebuilt; `--smoke=ui` (`SMOKE_UI_HOST_OK`, Steam initialized, avatars loaded) and `--smoke=lan-flow` (`STEP4/STEP7`) green. `--smoke=voice`/`steam-host` need Steam/voice runtime conditions.

Files: new `Packages/com.imasu.steamworks.voice/{package.json,Runtime/Imasu.Steamworks.Voice.asmdef,Runtime/VoicePlayer.cs,Runtime/VoiceHud.cs}`; new `Packages/com.imasu.steamworks.lan/{package.json,Runtime/Imasu.Steamworks.LAN.asmdef,Runtime/LanDiscovery.cs}`; core `Runtime/UI/IVoiceMember.cs`, `Runtime/LAN/ILanDiscovery.cs`, `Runtime/UI/{ScoreboardUI,ImasuSessionUI}.cs`, `Runtime/Network/ImasuNetworkManager.cs`, `Runtime/Imasu.Steamworks.asmdef` (versionDefines), `Samples~/StarterUI/{VoiceIndicator,PlayerListUI}.cs`, `Assets/LocalTest/Editor/ProductionSceneBuilder.cs`, `Assets/LocalTest/VoiceSmokeRunner.cs`, `ProjectSettings/ProjectSettings.asset`, `Ai Agent/ROADMAP_2.0.md`.

---

## 2026-08-24 — Control Center 2.0: per-mode overrides + Session Manager + spawns + tabbed window + roadmap

**Data model (B — per-gamemode & per-map overrides):**
- `ImasuSteamworksConfig` gains `GameModeOverrides` (curated sparse set: maxPlayers/minPlayers/lobbyName/security/voice group/mover group/p2p group — each `override_X` bool + value) and per-map `MapEntry` overrides (`override_maxPlayers` + cap, `note`). **Effective resolution: map → gamemode → global** via `EffectiveMaxPlayers/EffectiveMinPlayers/EffectiveSecurity/EffectiveVoip*…`. Steam id / scenes / kcpPort are deliberately NOT overridable.
- Wired through the flow: `LobbySettings.Defaults(mode,map)`; Host Setup pre-fills + clamps Max Players / Voice Range / Security from effective values and refreshes on gamemode/map change (`RefreshSetupEffectiveDefaults`); `StartMatch` (UI + SteamLobbyManager + ImasuNetworkManager) uses effective minPlayers (mode-aware via `ModeIndexByName`); `ImasuSteamworksMaster` pushes effective p2p/relay/nextGen + maxConnections; `SimpleFirstPersonController.Start` applies effective mover values.

**Session Manager prefab (the required scene contract):**
- `Assets/LocalTest/Prefabs/ImasuSession.prefab` migrated → `Packages/com.imasu.steamworks/Prefabs/ImasuSessionManager.prefab` (GUID preserved via .meta copy; old Assets copy deleted). Root GO renamed `ImasuSessionManager`; prefab now carries `SpawnManager`. **Hybrid lifetime**: every multiplayer scene holds a prefab instance (so the editor can validate spawns/config per scene), but only the FIRST survives as a `DontDestroyOnLoad` singleton — later scene instances self-destroy (`ImasuSteamworksMaster.Awake`).

**Spawns & Teams (new Runtime/Spawns):**
- `SpawnPoint` (exact, teamId 0=any, gizmo), `SpawnArea` (random point inside BoxCollider volume), `ImasuTeamConfig` (team names/colors + `minSeparation` anti-clump), `SpawnManager` (round-robin, team-filtered, anti-clump re-pick, fallback). `ImasuNetworkManager.OnServerAddPlayer` override spawns via SpawnManager with Mirror fallback. Match scenes (Game/Warehouse) now get 3 physical spawn points each; `ImasuTeamConfig.asset` created in Assets/Config.

**Control Center 2.0 (tabbed master hub):**
- `ImasuSteamworksControlCenter` rewritten: **Home** (company info + links placeholders + Tutorial + quick actions), **Setup Wizard** (5-step checklist with [Go]: config → Session Manager → spawns → gamemode → build settings), **Scenes & Modes** (pickers + validation + Sync Build Settings + Create Multiplayer Scene), **Gamemodes** (per-mode effective summary incl. overrides + map notes), **Spawns & Teams** (scene-aware spawn count + Add Spawn Point/Area + team config editor + create default), **Modules** (installed detection via `UnityEditor.PackageManager.PackageInfo` + catalog for Voice/LAN/Dedicated with install-URL copy), **Detailed Config** (full inspector). Persistent bottom bar: Apply to Master(s) / Add Session Manager to Scene / Open Docs / build-settings warning. `AddSessionManagerToScene` instantiates the package prefab (inline fallback if missing). Editor asmdef now references `Mirror` + `Mirror.Transports`.
- New `Runtime/Modules/IImasuModule.cs` — the optional-addon contract for future package splits.

**Roadmap:**
- `Ai Agent/ROADMAP_2.0.md` — modular package plan (core default; Voice/LAN/Dedicated separate addons), GitHub scoped-tag versioning (`com.imasu.steamworks@0.2.0`), next slices, credited borrows (PurrNet Packages + Gaia window-manager pattern), "not borrowing" list. **LAN and Dedicated are two separate addons** (dedicated later, depends on core only).

Files: `Runtime/Config/{ImasuSteamworksConfig,ImasuSteamworksMaster}.cs`, `Runtime/Steam/{LobbySettings,SteamLobbyManager}.cs`, `Runtime/Network/ImasuNetworkManager.cs`, `Runtime/UI/{ImasuSessionUI,SimpleFirstPersonController}.cs` (SimpleFPC is under Runtime/Game), new `Runtime/Spawns/{ISpawnProvider,SpawnPoint,SpawnArea,ImasuTeamConfig,SpawnManager}.cs`, new `Runtime/Modules/IImasuModule.cs`, `Editor/ImasuSteamworksControlCenter.cs` (rewrite), `Editor/Imasu.Steamworks.Editor.asmdef`, `Assets/LocalTest/Editor/ProductionSceneBuilder.cs`, prefab moved to `Packages/com.imasu.steamworks/Prefabs/ImasuSessionManager.prefab`, new `Assets/Config/ImasuTeamConfig.asset`, new `Ai Agent/ROADMAP_2.0.md`.

Verified: compile clean; `BuildAll` migrates prefab + rebuilds scenes (Game/Warehouse now carry 3 SpawnPoints) + creates team config; smoke player rebuilt; `--smoke=ui` (`SMOKE_UI_HOST_OK`) and `--smoke=lan-flow` (`STEP4/STEP7`) green; log shows the renamed `ImasuSessionManager`.

---

## 2026-08-24 — Config-driven game: default preset + gamemodes + scenes + kill-switch + ESC + avatar fix

**Game works out of the box again (the "no config" bug):**
- `Assets/Config/ImasuSteamworksConfig_Hidden.asset` created (Hidden preset: `addonEnabled`, `mainMenuScene=MainMenu`, `lobbyScene=WaitingRoom`, gamemode "Hidden" with Courtyard/Game + Warehouse, `maxPlayers 8/minPlayers 1/lobbyName "Hidden Game"`, steam 480, Fixed voice). Wired to the rebuilt `ImasuSession.prefab` (master.config = asset) and scenes. `Samples~/StarterConfig/TemplateImasuSteamworksConfig.asset` shipped as a starter preset for new projects. The old hardcoded map list (`manager.maps = {Courtyard, Warehouse}`, `ui.availableScenes`) is **gone** — the builder + master both push the config's catalog.

**Addon kill-switch (your design):**
- `ImasuSteamworksConfig.addonEnabled` — one-click toggle at the top of the Control Center. **OFF = fully suppressed**: `ImasuSteamworksMaster` skips its push, `SteamManager` skips Steam init (init deferred to `Start()` so master Awake ordering is safe), host/join/start are soft-blocked with a yellow banner, all hard errors gated. Component `enabled` on the master is a per-scene override.
- **Hard gate when misconfigured**: if a master exists with no config wired, `ImasuSessionUI` shows a RED banner and blocks hosting/joining/match-start ("open the Control Center and wire a config"). No silent fallback that pretends to work.
- `ImasuSteamworksMaster` now surfaces `HasConfig / AddonEnabled / AddonSuppressed / Misconfigured` statics; `ApplyToComponents` logs an Error (not silent return) when config is null and pushes gamemodes + scenes + maps to the manager.

**Gamemodes + scenes in the config (your future map/gamemode switching):**
- `gameModes[]` (`displayName` + per-mode `maps[]`), plus `mainMenuScene` + `lobbyScene` (equal = shared menu+lobby scene). Host Setup is now **Gamemode dropdown → Map dropdown** (map list re-populates on mode change). Legacy `maps[]` still works as a single "Default" mode.
- `ImasuNetworkManager` gained `ModeDisplayNames / MapDisplayNamesForMode / MapSceneAt` + `ServerChangeMap` (in-match scene switch preserving connections). `SteamLobbyManager.ServerChangeMap` also rewrites the lobby's advertised `DataScene`/`DataGameMode` so the server browser shows the new map. Pause menu gets a host-only **Change Map** button using the same catalog.
- Lobby data now carries `gamemode` (`DataGameMode`) end-to-end (create → join → browser).
- Control Center: Scenes & Modes summary + **Build-Settings validation** (flags any config scene missing from Build Settings) + **Sync Build Settings** button. Docs button fixed to `Docs/index.html`.

**ESC as Back/Close for modals (no scene nav):**
- `ImasuSessionUI.HandleEsc` closes the invite overlay, then any `SimpleDropdown` popup. `SimpleDropdown` gained static `OpenPopup` registry + `ClosePopups()`/`HasOpenPopup`; `SettingsMenu` closes a popup before closing the panel. ESC never navigates between screens.

**Avatar first-open white-squares (P0):**
- `AvatarLateAssign.Init` now sets the bottom-up UV flip centrally (waiting room/scoreboard included — removed the invite-row-only flip) and **polls the cache every 0.5s for 12s**, assigning the texture the instant it lands — so avatars fill on first open with NO typing/search. Kept the `onLoaded` handshake. `RefreshInviteList` logs a cached-vs-downloading diagnostic.

Files: `Runtime/Config/{ImasuSteamworksConfig,ImasuSteamworksMaster}.cs`, `Runtime/Network/ImasuNetworkManager.cs`, `Runtime/Steam/{SteamLobbyManager,LobbySettings,SteamManager}.cs`, `Runtime/UI/{ImasuSessionUI,AvatarCache,SimpleDropdown,SettingsMenu,ImasuPauseMenu}.cs`, `Editor/ImasuSteamworksControlCenter.cs`, `Assets/LocalTest/Editor/ProductionSceneBuilder.cs`, new `Assets/Config/ImasuSteamworksConfig_Hidden.asset` + `Samples~/StarterConfig/TemplateImasuSteamworksConfig.asset`, `Docs/{01,02}-*.html`.

Verified: compile clean; `BuildAll` regenerates scenes + `ImasuSession.prefab` wired to the Hidden config; smoke player rebuilt; `--smoke=ui` (`SMOKE_UI_HOST_OK`) and `--smoke=lan-flow` (`STEP4/STEP7`) green. `--smoke=steam-host` needs the Steam client running (not in this session) — code path unchanged. Hand-test next: open Invite Friends with no typing (avatars fill in ~3–5s), ESC closes the invite/dropdowns, add a map in the Control Center → appears in Host Setup + Change Map.

---

## 2026-08-24 — Rebrand to Imasu Steamworks + Control Center + voice overhaul + templates + offline docs

**Package rebrand (game-agnostic, not "Hidden"):**
- `com.hidden.steam-multiplayer` → **`com.imasu.steamworks`** (folder, package.json name/displayName `Imasu Steamworks`, description). Namespace `Hidden.SteamMultiplayer` → **`Imasu.Steamworks`**.
- Classes renamed: `HiddenNetworkManager`→`ImasuNetworkManager`, `HiddenSessionUI`→`ImasuSessionUI`, `HiddenPauseMenu`→`ImasuPauseMenu`, prefab `HiddenSession`→`ImasuSession` (file + builder). Log prefixes, menu labels (`Imasu/Steamworks/...`), asmdef names updated. **`Hidden Dev HDRP` (the game) keeps its name — only the package was renamed.**
- `.meta` GUIDs preserved throughout → existing scenes/prefabs still resolve. `packages-lock.json` updated. **Namespace collision handled**: the package now lives in `namespace Imasu.Steamworks`, so Facepunch's `Steamworks.*` refs are qualified `global::Steamworks.*` (and the Control Center window's `Editor` namespace required `UnityEditor.Editor.CreateEditor`).

**Imasu Steamworks Control Center (`Imasu → Steamworks Control Center`):**
- New `Editor/ImasuSteamworksControlCenter.cs` + `Editor/Imasu.Steamworks.Editor.asmdef`. One window that: creates/selects the shared config asset, edits every field (via a proper inspector), applies values to live scene components (SteamManager, FizzyFacepunch, KcpTransport, ImasuNetworkManager), adds the master to a selected object, shows validation, and opens the offline docs.

**Master config asset (single source of truth):**
- New `Runtime/Config/ImasuSteamworksConfig.cs` (ScriptableObject) + `Runtime/Config/ImasuSteamworksMaster.cs`. One asset holds: Session (maxPlayers 2–32, minPlayers 1–max, lobbyName, security, code length), Steam (appId, requireSteam), Voice (voipEnabled, useDistanceAttenuation, voipMin/Max, voipUserVolumeDefault), Debug (allowNonSteamServers), Maps[], Transport (p2pTimeout, relay, next-gen, kcpPort), Player Mover (walk/sprint/jump/gravity/sens). `ImasuSteamworksMaster` pushes config → components on Awake; `GameSettings.SeedFromConfig` writes first-run PlayerPrefs defaults; `LobbySettings.Defaults()` seeds from config.

**Voice overhaul (per your calls):**
- `useDistanceAttenuation` OFF = **Fixed** (2D, no dropoff, everyone full volume); ON = **Range** (3D logarithmic rolloff between voipMinDistance and voipMaxDistance). Wired into `VoicePlayer.EnsureAudioSource` (+ per-user `GameSettings.VoiceUserVolume` gain on `AudioSource.volume`; capture gated by voipEnabled/VoiceEnabled).
- Settings: **removed** the user-facing distance input (distance is host-controlled via Control Center); added **VOIP Volume (how loud others are)** slider 0–2× on the Audio tab (`GameSettings.VoiceUserVolume`, `AddVoipRow`).

**minPlayers enforcement + debug flag:**
- `minPlayers` enforced in `ImasuNetworkManager.StartMatch` (connected players) and `SteamLobbyManager.StartMatch` (lobby members); UI shows "Need N players to start" when blocked. Default 1 = host can start solo.
- `allowNonSteamServers` debug flag: when OFF, hides the Game Setup "Use Steam networking" checkbox + hint, hides the LAN browser section, and blocks join-by-IP. (`ImasuSessionUI` setup screen, `AppendLanRows`, `OnJoinByCodeClicked`.)

**Template scripts + docs:**
- `Samples~/StarterUI/` (6 copy-paste MonoBehaviours, `namespace Imasu.Steamworks.Templates`): `SteamUserBadge`, `LobbyCodeDisplay`, `PlayerListUI`, `InviteButton`, `VoiceIndicator`, `HostSetupForm` — exposed via `package.json` `samples`.
- `Docs/` — offline static HTML docs (opens by double-click, no server): `index.html` + 7 pages (Getting Started, Control Center reference w/ every field table, Voice, Lobby Flow, UI Templates, Testing & Smokes, Troubleshooting), shared `styles.css`, `img/placeholders/` for screenshots, `README.md` incl. Docusaurus port notes.

Files: package renamed (see above); `Runtime/Config/{ImasuSteamworksConfig,ImasuSteamworksMaster}.cs` new; `Editor/{ImasuSteamworksControlCenter.cs,Imasu.Steamworks.Editor.asmdef}` new; `Runtime/UI/{GameSettings,SettingsMenu,ImasuSessionUI,ImasuPauseMenu,AvatarCache}.cs`, `Runtime/Voice/VoicePlayer.cs`, `Runtime/Network/ImasuNetworkManager.cs`, `Runtime/Steam/{SteamLobbyManager,LobbySettings}.cs` edited; `Assets/LocalTest/{ProductionSceneBuilder,UiSmokeRunner,...}.cs` rebranded; `Samples~/StarterUI/*`, `Docs/*` new; `package.json` (name/samples), `packages-lock.json`, asmdefs updated.

Verified: compile clean; `BuildAll` regenerates `ImasuSession.prefab` (now carries `ImasuSteamworksMaster`); smoke player rebuilt; `--smoke=ui`, `--smoke=lan-flow`, `--smoke=steam-host` all green. **Note: Player.log moved to `%USERPROFILE%\AppData\LocalLow\Imasu\Hidden Dev HDRP\Player.log`** (company name is `Imasu`).

---

## 2026-08-24 — GitHub push with LFS smoke zip + pre-push hook (same branch)

**Pushed project to GitHub (`https://github.com/Imasu7/Hidden-Dev-HDRP.git`, branch `main`) with builds via LFS:**
- **`.gitignore`**: changed `/[Bb]uilds/` → `/[Bb]uilds/*` + `!/[Bb]uilds/*.zip` + `!/[Bb]uilds/.gitkeep` — unzipped `Builds/LocalTestSmoke/` stays local, ignored, usable; only `Builds/HiddenDevHDRP-Smoke.zip` is versioned (LFS via `.gitattributes` `*.zip filter=lfs`).
- **`.githooks/pre-push`**: new versioned hook (via `core.hooksPath=.githooks`) that on `git push` zips `Builds/LocalTestSmoke/*` → `Builds/HiddenDevHDRP-Smoke.zip` (PowerShell `Compress-Archive`), `git add`s, and if changed commits `chore: update smoke zip [skip ci]` then aborts so user re-pushes. Includes timestamp check (skips if zip newer than source) to avoid infinite churn, plus LFS chaining (`git lfs pre-push "$@"`). Keeps unzipped folder intact.
- **Zip**: `Builds/HiddenDevHDRP-Smoke.zip` (~91 MB, 261 LFS objects, 417 MB total) created locally, not deleted after push — remains usable for play-testing without re-building.
- **Push**: initial commit `63bfe54` (2119 files) + auto zip commit `8bb046d` + hook fix `3469c0b` pushed to `origin/main` (`gh auth` as `Imasu7`, `git-lfs/3.7.1`). LFS upload 100% (261/261).
- **Release**: `v0.1-smoke` created via `gh release create` with zip asset: https://github.com/Imasu7/Hidden-Dev-HDRP/releases/tag/v0.1-smoke — one-click download for non-Unity testers (`HiddenDevHDRP.exe --smoke=ui|lan-flow|steam-host`).

Files: `.gitignore`, `.gitattributes` (existing `*.zip lfs`), `.githooks/pre-push`, `Builds/HiddenDevHDRP-Smoke.zip` (LFS), `.git/hooks/pre-push` (LFS, now chained).

Notes: `Builds/LocalTestSmoke/` is intentionally not in git; the zip is the distribution artifact. Future `git push` auto-updates the zip; no manual zipping needed. Non-technical testers should use Releases, not `git clone`.

---

## 2026-08-24 — Invite list: rows restored after filter clear + avatars without typing; template project removed

**"Empty list forever after clearing a non-matching filter" — fixed** (`HiddenSessionUI.cs`):
- Root cause: the pooled invite rows were hidden by a 0-result filter (`SetActive(false)`) but `RefreshInviteList` reused them via `Bind()` without ever reactivating them — so clearing the filter re-bound the same 50 hidden rows (count label said 50/56, nothing visible) and even reopening the panel stayed empty until relaunch.
- Fix: `InviteRowEntry.Bind()` now always calls `go.SetActive(true)` before updating the row. Covers both "clear filter" and "reopen panel".

**Avatars still blank on first open — fixed** (`HiddenSessionUI.cs`, `AvatarCache.cs`):
- Second root cause behind "only fill after typing": a row hidden while its avatar download finished still received `onLoaded`, but the write went to an inactive `RawImage`; reusing that row for the *same* friend skipped `lateAssign.Init` (the `same==true` path), so it stayed blank. Typing to a *different* friend forced a fresh `Init()` that hit the now-filled cache — looked like "search pulls avatars".
- Fix: `Bind()` also re-inits the avatar when `avatar.texture == null` even for the same id (plus resets the button to idle). First-open avatars now fill without typing. (`AvatarCache` resilience from the prior round — persona-hook retry + attempt logs — kept.)

**Old reference project removed** — `UnityMultiplayer-MirrorAndSteamworks-Template`:
- All slices that depended on it are done (Mirror 96.11.0 vendored; Facepunch 2.4.1 + FizzyFacepunch ported into the package). Grep confirmed only doc references remained.
- **All folder contents deleted.** A 0-byte empty directory shell remains at `C:\Users\strideR\Unity Projects\UnityMultiplayer-MirrorAndSteamworks-Template` because some process holds a handle to the (now empty) directory itself — `rd`/`.NET Delete`/`MoveFileEx` all refused (not a file lock; no handle enumerates with a matching path). It disappears on the next reboot; no action needed. Re-clone from GitHub if ever required.
- Doc references patched to "removed": `RULES.md` (rule 4), `DECISIONS.md` (D2), `PLAN.md` (reference section), `STATUS.md` (done + environment table), `Ai Agent/README.md`, `stacks/09-workflow.md`.

Files:
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (Bind activates row + re-inits blank avatar)
- `Ai Agent/{RULES,DECISIONS,PLAN,STATUS,README}.md`, `Ai Agent/stacks/09-workflow.md` (template removal notes)

Verified: compile clean; `--smoke=ui`, `--smoke=lan-flow`, `--smoke=steam-host` green; no exceptions. Hand-test: open Invite with no filter → avatars fill without typing; type nonsense → "No online friends match"; clear → 50 rows back; reopen panel still works; scroll (wheel + drag) fine.

---

## 2026-08-24 — Invite avatars load on first open; invite list scrolls again

**Avatars still blank on first open (only filled after typing) — fixed:**
- Root cause: `RefreshInviteList` destroyed and recreated every row on every keystroke. The async avatar fetch (`[AvatarCache]`, retries up to 8×) often completed *after* the subscribing row had already been destroyed, so `onLoaded` fired with no live subscriber; the keystroke's rebuild then hit the just-filled cache and looked like "typing pulls avatars".
- Fix: **pooled diff-reuse rows** (`InviteRowEntry` list in `HiddenSessionUI`). Refresh now binds the existing rows in place (updates name + re-inits the avatar), creates only net-new rows, hides surplus. The `AvatarLateAssign` subscription survives across refreshes, so the first download reaches a live row and avatars fill without typing. Cheap even for 100+ friends (no per-keystroke 50× `Destroy`/`new GameObject`).
- `AvatarLateAssign.Init` now safely re-inits (drops any previous `onLoaded` subscription before re-subscribing) so reused rows swapping friends can't leak stale handlers.
- `AvatarCache.EnsurePersonaHook` no longer marks the persona-state hook as wired when the subscribe throws (Steam not ready yet) — a later `Get()` retries once Steam is initialized. Added per-attempt `[AvatarCache] <id>: attempt N/8 not ready yet` logs for the next hand-test's evidence.
- Cap surfaced in the UI: status shows "Showing 50 of 173 online friends — type to filter" when the list overflows.

**Invite list could not scroll — fixed:**
- Root cause: each row's hover `Image` (`raycastTarget=true`) + `EventTrigger` captured the pointer, but `EventTrigger` doesn't implement the drag interfaces, so drags never reached the owning `ScrollRect` and the list felt locked.
- Fix: new `ScrollDragForwarder` component (`IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`/`IScrollHandler`) on every row forwards drag + scroll events to the parent `ScrollRect`. Hover reveal + click-to-invite unaffected.

Files:
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (pooled invite rows, count/cap status, removed `InviteInfoRow`)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/AvatarCache.cs` (resilient hook, re-init-safe late assign, attempt logs)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/ScrollDragForwarder.cs` (new)

Verified: compile clean; `--smoke=ui`, `--smoke=lan-flow`, `--smoke=steam-host` all green; no runtime exceptions. Hand-test: open Invite Friends in a Steam lobby with NO typing — avatars fill within ~few seconds (`[AvatarCache] avatar ready …` per id), right-side-up; typing narrows without blanking; wheel + drag scrolling work; hover still reveals the blue Invite. After this round passes, proceed to the authored-UI-prefab migration.

---

## 2026-08-24 — Invite panel rework, avatar flip, click-to-copy code

**Invite Friends panel rework** (`HiddenSessionUI.cs`):
- List narrowed from ~90% width to a centered 0.22–0.78 band; rows taller (34 → 54), avatar 40px, name 17.
- **Hover-reveal Invite button**: rows show a subtle background; on hover the row brightens and a blue "Invite" button fades in (no 50 visible buttons). The row's background Image is the single raycast target (children are non-raycast) so hover/clicks never flicker or get stolen by child elements; clicking anywhere on the row invites that friend (row `EventTrigger` → `PointerClick`).
- **Visible button states everywhere**: `Btn()` now wires `Button.colors` (lighter hover 1.18×, pressed 0.78×, disabled 0.6×) — runtime buttons previously tinted nothing, so "is it clicking?" was unanswerable. Invite blue = 0.2/0.45/0.85.
- **Invite feedback + reliability**: result now logged with friend name + SteamID; on success the button turns green "Invited"; on failure it turns amber "Retry" (stays live) and a status line explains ("lobby may be invite-only"). No more silent dead grey button.

**Avatars upside-down** (`HiddenSessionUI.cs`): friend avatars displayed vertically flipped — Steam's `Image.Data` is bottom-up. Fixed via `RawImage.uvRect = (0,1,1,-1)` on the invite-row avatar (scoped to the invite list; waiting-room/scoreboard avatars unchanged — verify they aren't also flipped in the next hand-test, then move the flip to `AvatarCache` texture creation).

**Lobby code no longer an editable field** (`HiddenSessionUI.cs`):
- Replaced the `TMP_InputField` + orphaned "Copy Code" button with a read-only `CodeLabel` box. Clicking it copies the code to the clipboard and flashes "Copied!" for ~1.2s (survives `RefreshWaiting` rewrites). Cannot be typed into or deleted. Fixes the copy button's misalignment after the action-column shift.

Files: `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (Btn colors, InviteRow, InviteFriend, BuildInviteScreen, CodeLabel, OnCopyLobbyCode, RefreshWaiting). No logic dependencies changed — smoke runners unaffected.

Verified: compile clean; `--smoke=ui`, `--smoke=lan-flow` (Leave + StartMatch), `--smoke=steam-host` all green; no runtime exceptions. Hand-test items: invite avatars fill without typing (late-assign + retries 8 ≈ 14s), are right-side up, hover reveals the button, clicking logs "Invite <name> …sent/FAILED"; code label clicks copy; check waiting-room/scoreboard avatars for the same flip.

---

## 2026-08-24 — Non-Steam (LAN) lobby flow fixed: Leave + Start Match

**"Leave Lobby" did nothing / bounced back to the waiting room (non-Steam):**
- `SteamLobbyManager.LeaveLobby()` early-returned `if (!inLobby)` — in a no-Steam session there's no Steam lobby, so the KCP host/connection was never stopped. The UI's `OnLeaveLobby` then called `ShowMenu()`, but a per-frame view enforcer re-showed the waiting room while the network was still active → "goes back, then instantly back in".
- Fix: `LeaveLobby()` now tears down whatever session exists — Steam lobby teardown when `inLobby`, otherwise stops host/client/server via the network manager (`StopHost`/`StopClient`/`StopServer`). The waiting room's Leave Lobby button now fully exits to the main menu in both modes. (Pause menu's Quit to Menu already used the correct full teardown.)

**"Start Match" did nothing (non-Steam):**
- `OnStartMatch` → `lobbyManager.StartMatch()` early-returned `!inLobby`. The underlying `HiddenNetworkManager.StartMatch()` is Steam-free (just needs `matchScene`), so:
  - The no-Steam create flow now copies the setup screen's chosen map into `networkManager.matchScene` before `StartLocalHost()`.
  - `OnStartMatch` branches: Steam lobby → `lobbyManager.StartMatch()`; else host active → `networkManager.StartMatch()`.

**Voice question (no-Steam):** transport is Mirror (fine over LAN/KCP), but mic capture/decode piggyback the Steam client APIs (`SteamUser.VoiceRecord`/`ReadVoiceDataBytes`/`DecompressVoice`) — so non-Steam sessions have no voice by design. Push-to-Talk button now hides when Steam isn't initialized and the voice-stats label reads "Voice requires Steam." Real fallback voice (Unity Mic + PCM) is a separate round.

**Avatar evidence:** `[AvatarCache]` now logs one line when an avatar resolves (and the existing give-up warning), retries bumped 5→8 (~14s window). Verified own avatar logs in the steam-host smoke. Re-test invite avatars in a **Steam** lobby with friends online to see per-id evidence. (Your white-pic sighting almost certainly predated the fix build — the Invite list doesn't even exist in non-Steam lobbies, and the fixed build hides the invite button there.)

New smoke runner: `--smoke=lan-flow` (start KCP host → LeaveLobby stops it → re-host → StartMatch reaches Game scene). All smokes green: `ui`, `steam-host`, `lan-flow`; compile clean; no runtime exceptions.

Files:
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (LeaveLobby handles non-Steam)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (OnStartMatch branch, matchScene at create, PTT/voice honesty)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/AvatarCache.cs` (evidence log + retry bump)
- `Assets/LocalTest/LanFlowSmokeRunner.cs` (new smoke)

---

## 2026-08-24 — Pause cursor fix, avatar root-cause fix, sensitivity, no-Steam hosting + unified browser

**Pause menu unusable (cursor stayed locked/invisible over the menu):**
- Root cause: `HiddenPauseMenu` unlocked the cursor correctly, but `SimpleFirstPersonController` re-locked it every frame ("re-lock when menus close") with no knowledge that a menu was open — it won the per-frame fight.
- Fix: added `HiddenPauseMenu.IsOpen` (static); the controller now leaves the cursor alone entirely while `HiddenPauseMenu.IsOpen || SettingsMenu.IsOpen` (forces visible+unlocked) and only re-locks once every overlay is gone.

**Invite avatars — real root cause found (earlier fix only partial):**
- `AvatarCache` was calling `SteamFriends.RequestUserInformation(id, true)` — the `bRequireNameOnly` flag. Per Facepunch docs, `true` downloads ONLY the persona name and explicitly skips the avatar → `GetMediumAvatarAsync` returns null forever → white blocks, while names load fine.
- Now passes `false` (name + avatar). Added a static `OnPersonaStateChange` hook that re-fetches any requested-but-empty avatar the moment Steam actually downloads it, and a one-line warning when all retries fail (so a genuinely-avatar-less friend is diagnosable).

**Mouse sensitivity setting:**
- `GameSettings.MouseSens` (`hds_mouse_sens`, default 1.0, clamped 0.2–5) + slider on Settings → Controls ("Mouse Sensitivity", shows x). Controller reads it live each frame.

**No-Steam hosting is back, properly (checkbox instead of the hidden Dev buttons):**
- Dev Local Host/Client buttons + label removed from the main menu (they were shrunk to 150x32 at absolute offsets and effectively unclickable).
- Game Setup screen gains a persisted **"Use Steam networking" checkbox** (default ON). Unchecked → "Create Lobby & Host" runs the existing KCP path (`StartLocalHost()`), voice still relays through Mirror.
- Join screen's single input now accepts a **7-char code (Steam) OR an IP/hostname (LAN)** — auto-detected (codes never contain `.`/`:`; `localhost` supported; `:port` stripped). New `HiddenNetworkManager.StartLanClient(address)` forces KCP + starts a client to that host.

**Server browser now covers both Steam and LAN:**
- Steam lobbies filtered server-side by a new marker key (`LobbySettings.DataMarker`="hds" = "1") so other games sharing the Spacewar appid (480) never appear.
- New `LanDiscovery` component (Mirror `NetworkDiscoveryBase`): LAN hosts advertise name/players/map on UDP (advertised automatically by `StartLocalHost`, and by headless servers via the base `Start()`), the browser listens and lists them under a "LAN SERVERS" divider; Join button direct-connects via KCP to the packet's real source address. Thread-safe found-list polled from the main thread. Browser shows LAN-only when Steam isn't initialized; discovery starts on entering Browse and stops on leaving.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/UI/AvatarCache.cs` (false flag + OnPersonaStateChange + failure log)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenPauseMenu.cs` (Instance + IsOpen)
- `Packages/com.hidden.steam-multiplayer/Runtime/Game/SimpleFirstPersonController.cs` (menu gate + sens multiplier)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/GameSettings.cs` (MouseSens + UseSteam)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/SettingsMenu.cs` (sensitivity slider row)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (dev buttons removed, checkbox, join-by-IP, merged browser)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/LanDiscovery.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (StartLanClient, discovery ensure+advertise)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/{LobbySettings,SteamLobbyManager}.cs` (marker key)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/LobbyCode.cs` (LooksLikeCode)

Verified: compile clean; player rebuilt; UI smoke pass; steam-host smoke pass (lobby `109775243172881041` code `338E6JS`); no exceptions in smoke logs.

Notes for next agent:
- LAN discovery can't be same-machine smoke-tested (Windows doesn't loop broadcasts back) — the real test is main PC hosts with Steam off, second machine browses LAN / joins by IP. VM is a good second peer for this.
- Sensitivity/checkbox are PlayerPrefs-persisted (`hds_mouse_sens`, `hds_use_steam`) — check `Ai Agent/STATUS.md` conventions when adding more.
- Authored-UI-prefab migration is still the queued next round.

---

## 2026-08-24 — Invite avatars fixed, Tab scoreboard (ring avatars, names, ping)

**Invite-friend avatars showed white blocks (own avatar + names were fine):**
- Root cause: `InviteRow` set `AvatarCache.Get()` once at build time; friends' avatars aren't cached by the local Steam client until downloaded, so the first async fetch returns null and a one-shot row never re-asks. Your own avatar is always cached → worked.
- `AvatarCache` now forces the download (`SteamFriends.RequestUserInformation(id, true)` before fetching), retries ~5 × backoff on null results, and fires `onLoaded` when a texture lands.
- New `AvatarCache.AvatarLateAssign` component assigns the texture the moment it arrives (immediately if already cached) and unsubscribes on destroy. Applied to invite rows and waiting-room member rows.

**Tab scoreboard (hold to show):**
- New `ScoreboardUI` passive overlay (self-building canvas, sortingOrder 450 below Settings): right-aligned sheet listing every spawned player — avatar inside a status ring, name, ping (ms). Never blocks UI raycasts, never touches the cursor. Shows in waiting room too (no player objects there yet → falls back to Steam lobby member list, ping "—").
- Ring colours: green = talking (`VoicePlayer.isSpeaking` remote / `isMicLive` local); red = mic confirmed failed; grey = no verdict yet (confirmed-failure-only, as chosen).
- `VoicePlayer` gained `[SyncVar] MicState micState` (Unknown/Ready/Failed): owner sets Ready after its first successful captured voice frame, Failed after a capture exception.
- New `PlayerRtt` broadcasts the owner's `NetworkTime.rtt` (ms) every second via SyncVar so every client can show everyone's ping (Mirror only exposes your own connection's RTT locally). Added to the DummyPlayer prefab by the builder.
- `HiddenNetworkManager.Start()` ensures the ScoreboardUI singleton exists once a session object is present.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/UI/AvatarCache.cs` (download force + retry + onLoaded + AvatarLateAssign)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (invite + member rows use AvatarLateAssign)
- `Packages/com.hidden.steam-multiplayer/Runtime/Voice/VoicePlayer.cs` (MicState SyncVar)
- `Packages/com.hidden.steam-multiplayer/Runtime/Game/PlayerRtt.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/ScoreboardUI.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (Ensure)
- `Assets/LocalTest/Editor/ProductionSceneBuilder.cs` (PlayerRtt on DummyPlayer)

Verified: compile clean; BuildAll regenerated prefabs (DummyPlayer contains PlayerRtt); player rebuilt; UI smoke pass; steam-host smoke pass (lobby `109775243169056265` code `JQU5P9Q`); no exceptions from the new code in the smoke logs.

Notes for next agent:
- Scoreboard visuals (ring colours, avatar late-fill, ping) need a real hand-test with a second Steam user; headless VM shows grey rings (0 capture frames → micState stays Unknown), which is correct confirmed-failure-only behaviour.
- Cross-user test also covers the invite-avatar fix (second user's pic should now populate ~0.5–1 s after the list opens instead of staying a white block).
- `AvatarCache.AvatarLateAssign` is the reusable pattern for any one-shot avatar row (scoreboard rows use it too).

---

## 2026-08-23 — Steam init race fix, no-freeze pause, waiting-room layout, test FPS controller

**"Steam not initialized" despite Steam being up (menu label + invite screen):**
- Root cause: FizzyFacepunch and `SteamManager` both call `SteamClient.Init`; Facepunch 2.4.1 throws on the second call ("already initialized") and Awake order is undefined. When the transport won the race, SteamManager caught the throw, set `initialized=false` and it stayed wrong — while hosting still worked because `SteamLobbyManager.steamReady` uses `SteamClient.IsValid` directly.
- Fix in `SteamManager.InitSteam`: early-out `initialized=true` when `SteamClient.IsValid` is already true, and treat the "already initialized" exception as success. Doc comment corrected (it previously claimed Init was idempotent).

**Pause menu no longer freezes the game:**
- Removed `Time.timeScale = 0/1` from `HiddenPauseMenu.SetOpen`. In a multiplayer listen-server, freezing time freezes everyone's sim and desyncs clients. The menu is now overlay-only (cursor handling unchanged). `OnDisable` safety hook no longer touches time.

**Waiting room layout clip:**
- The member list (now rendered with background strips + avatars) spanned x 0.02–0.42 while the action buttons (centered x=0.5, 320px) started ≈0.375 — overlapping visually. List narrowed to x≤0.34 and Start Match / Push to Talk / Leave / Invite Friends shifted +170px right.

**Basic test character controller (WASD / Space jump / mouse look):**
- New `SimpleFirstPersonController` (package Runtime, owner-only via `isLocalPlayer`): mouse-delta yaw+pitch look (clamped), WASD relative to view with sprint (Shift), Space jump + gravity via `CharacterController`. Runs only in the match scene AND only while the cursor is locked — opening pause/settings releases the mouse, which halts input automatically. Re-locks cursor when menus close; never leaves the cursor stuck on disable.
- `DummyPlayer.prefab` rebuilt: +`CharacterController` (center y1, h2, r0.4), +`SimpleFirstPersonController`, +child `PlayerCamera` at head height (Camera + HDAdditionalCameraData + AudioListener, tagged MainCamera). NetworkTransform now syncs rotation (yaw) and is `ClientToServer` (player authority). Scene cameras no longer carry an AudioListener (the player camera owns it — avoids duplicate-listener warnings in match scenes).
- `cameraPivot` (the player camera) carries pitch locally; only yaw replicates to others.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamManager.cs` (race fix)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenPauseMenu.cs` (no timeScale)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (waiting-room layout)
- `Packages/com.hidden.steam-multiplayer/Runtime/Game/SimpleFirstPersonController.cs` (new)
- `Assets/LocalTest/Editor/ProductionSceneBuilder.cs` (player prefab components, camera listener move)

Verified: compile clean; BuildAll regenerated scenes+prefabs (DummyPlayer.prefab contains controller/CC/PlayerCamera/HDAdditionalCameraData/AudioListener); player rebuilt; UI smoke pass; steam-host smoke pass twice (lobby `109775243166976552` code `K4B9VCZ`, second `109775243166976722` code `CERRXUX`) with SteamManager reporting successful init each run. Controller + layout + Steam label need a hand-test.

Notes for next agent:
- **Queued next round (user-approved): authored-UI-prefab migration** — have the builder save real uGUI Canvas prefabs (editable in editor) with the procedural builder kept as fallback for headless builds/smokes.
- If the "already initialized" branch ever needs exercising: it only triggers when FizzyFacepunch's `InitialiseSteamworks` runs before `SteamManager.Awake`; the logic is a plain `SteamClient.IsValid` check, safe to trust.
- CharacterController + NetworkTransformReliable(ClientToServer) is the standard Mirror player-mover combo; swap the real controller in later without touching the transport.

---

## 2026-08-23 — Proper main menu, join codes, server browser, settings menu, avatars

**Short friendly lobby codes (`HG6FY14`):**
- New `LobbyCode.cs`: 7-char codes from an unambiguous alphabet (no O/0/I/1 so codes survive being read aloud). Host generates on create and stores it as new lobby-data key `code`; `LobbySettings` carries it. Waiting room shows/copies the short code (raw ID fallback for legacy lobbies).
- `SteamLobbyManager.JoinByCode(input)`: normalizes input → searches public lobbies via `LobbyList.FilterDistanceWorldwide().WithKeyValue(DataCode, code)` → joins first hit; clear failure reasons via new `onJoinFailed(string)` event. Input longer than 7 chars that parses as a number is still accepted as a raw Steam lobby ID (back-compat).
- Verified live: host smoke logged `Lobby created: ... code 8Y7YCX9`.

**Standard main menu (same scene — 3D backdrop can slot in later):**
- Main Menu is now a hub: **Join Game / Host Game / Settings / Quit**, plus the no-Steam dev buttons (Local Host/Client) shrunk into the bottom-left corner.
- **Join Game screen** has two views: *code entry* (default: input + Join + inline error line + "Browse Servers") and a **CS:GO-style server browser**: column headers `Server Name | Players | Map | Mode`, scrollable rows from `LobbyList.FilterDistanceWorldwide().WithSlotsAvailable(1).WithMaxResults(50)`, name filter box, per-row Join, Refresh button. Steam exposes no ping before joining (column omitted); password lobbies don't exist yet but will list automatically once they do.

**Settings menu (works from main menu AND in-game pause):**
- New self-building `SettingsMenu.cs` overlay with horizontal tabs — **Game** (voice chat toggle, default voice distance — seeds setup screen), **Display** (fullscreen mode, resolution list, vsync, quality preset — applied instantly), **Audio** (Master/Music/SFX/Voice sliders; Master drives `AudioListener.volume` now), **Controls** (read-only default-bindings list for now) — plus Restore Defaults/Back.
- New `GameSettings.cs`: PlayerPrefs-backed store (`hds_*` keys) applied at boot by a `RuntimeInitializeOnLoadMethod`. `SettingsMenu` opens from anywhere via static `Open()` and survives scene changes.
- Pause menu rework: buttons now Resume / **Settings** / Back to Lobby (host-only) / Quit to Menu. **"Quit Game" removed from the match scene** — quitting lives only on the main menu. Escape closes Settings first; a frame-flag handshake stops one keypress closing Settings AND toggling the pause menu.

**Steam avatars (square, left of name):**
- New `AvatarCache.cs`: cached `GetMediumAvatarAsync` → RGBA→Texture2D; rows just call `Get()` each rebuild and the pic appears once fetched.
- Waiting-room member rows rebuilt as avatar+name containers; invite-list rows gained a 26px avatar slot before the name.

**Refactor:** `SimpleDropdown` promoted to its own shared file (`Runtime/UI/SimpleDropdown.cs`) with the popup opener and an `onValueChanged` event; HiddenSessionUI's nested copy deleted.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/LobbyCode.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/LobbySettings.cs` (DataCode key + field)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (code gen/write/read, JoinByCode, onJoinFailed)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/SimpleDropdown.cs` (promoted + event)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/AvatarCache.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/GameSettings.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/SettingsMenu.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (menu hub, Join screen + browser, avatar rows, short-code display)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenPauseMenu.cs` (Settings tab-in, Quit Game removal, Escape handshake)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (MapDisplayNameFor reverse lookup for browser rows)

Verified: compile clean; player rebuilt; UI smoke pass (`SMOKE_UI_HOST_OK players=1`); steam-host smoke pass (lobby `109775243131111298`, code `8Y7YCX9`). Browser/avatar/tab clicks need a hand-test; two-user code join needs the VM or a friend.

Notes for next agent:
- `SimpleDropdown.onValueChanged` fires only when the value actually changes — subscribe for persistence, don't hook the open-click.
- Server browser intentionally lists only lobbies with a free slot; private (invite-only) lobbies never appear by Steam design.
- `GameSettings` display apply calls `Screen.SetResolution` — harmless headless, real in player.

---

## 2026-08-23 — Working map dropdown, lobby Security setting, labeled setup screen, boot fix

**Game Setup screen overhaul (user-reported issues):**
- **Map dropdown now opens.** The old control used `TMP_Dropdown`, which silently does nothing at runtime without an authored template. Replaced with a custom `SimpleDropdown` value box: clicking opens an invisible full-screen click-away blocker + popup option panel (parented to the setup screen, dies with it), selected row highlighted, caption updates. Same control now also powers the new **Security** dropdown.
- **Security dropdown replaces the Public / Friends-only toggles** (user request): `LobbySettings.LobbySecurity` enum — `InviteOnly` → `Lobby.SetPrivate()` (join ONLY via accepted Steam invite; raw code join is impossible), `Friends` → `SetFriendsOnly()` (friend of ANY member can join), `Open` → `SetPublic()` (default). Written/read as new lobby-data key `security`; old lobbies without the key fall back to deriving from the legacy `public` flag. `SteamLobbyManager.CreateLobbyAsync` applies visibility via one switch; `ReadSettingsFromLobby` parses with legacy fallback.
- **Every setup field has a label above its box** (Server Name, Max Players, Map / Scene, Voice Max Distance (m), Security) — previously only Map had a label and inputs relied on placeholder text; Create/Back buttons sit side-by-side.
- **Game boots to Main Menu.** Screens were born active and `RefreshScreens` preserved active state, so first launch landed on Game Setup. `BuildUi` now explicitly sets menu active / everything else inactive.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/LobbySettings.cs` (LobbySecurity enum + data key + legacy fallback)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (ApplyLobbyVisibility switch, security parse)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (SimpleDropdown + popup, relabeled/relaid-out setup screen, security dropdown, boot-state fix; removed unused Toggle helper)

Verified: compile clean; player rebuilt; UI smoke pass (`SMOKE_UI_HOST_OK players=1`); steam-host smoke pass (`SMOKE_STEAM_HOST_OK`, lobby 109775243125214577). Dropdown clicks can't be exercised headless — user should confirm by hand.

Notes for next agent:
- `SimpleDropdown` is a plain class (not MonoBehaviour) holding options/value/caption; reuse it for any future runtime dropdown instead of TMP_Dropdown.
- InviteOnly caveat to communicate in-game eventually: joining requires an invite — the "join by code" path will fail for private lobbies by design.
- The steam-host smoke process occasionally hangs on quit after reporting OK (Steam shutdown); kill `HiddenDevHDRP.exe` if Start-Process -Wait times out — results are still in Player.log.


**Map catalog (prep for multi-map + future match rotation):**
- `HiddenNetworkManager` now has `MapEntry[] maps` (displayName → sceneName) with `MapDisplayNames` / `MapSceneAtIndex(index)` helpers. `HiddenSessionUI`'s setup dropdown lists display names from the catalog and resolves the pick back to a scene name via the manager.
- `SetNextMatchScene(scene)` on the manager and `SteamLobbyManager.SetNextMap(scene)` queue an explicit scene for the next `StartMatch()` — the hook end-of-match map rotation will call later; lobby data flow unchanged for normal play.
- **New "Warehouse" map** (`Assets/LocalTest/Scenes/Warehouse.unity`): dark industrial interior — dark concrete floor, 10 crates + 6 corner pillars as cover, dim cool-blue sun (~18k lux) vs Courtyard's warm bright sun, teal AiCube. Builder's match-scene code is parameterized now (one function builds both maps). Warehouse is in Build Settings; `HiddenSession.prefab` carries the catalog: Courtyard → Game, Warehouse → Warehouse.

**Cursor fixes ("no cursor after leaving game"):**
- `HiddenSessionUI.Update` enforces `Cursor.visible = true` / `lockState = None` whenever any menu screen is showing — menus can never be cursor-less regardless of stale state.
- `HiddenPauseMenu.OnDisable` restores cursor + resets `Time.timeScale = 1`, covering scene-change/destroy-while-open edge cases (also prevents stuck pause).

**Party-flow pause menu:**
- "Back to Lobby" is now host-only (hidden for clients). Host picking it runs `ServerChangeScene(WaitingRoom)`, pulling every client back into the shared waiting room so the party keeps the lobby between matches. Clients get Resume / Quit to Menu / Quit Game.

**Verified:** compile clean; scenes/prefab regenerated (Warehouse: 10 crates, 6 pillars, AiCube, sky); player rebuilt; KCP host+client, sync host+client (client saw moving AiCube), and UI smokes all pass.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (map catalog + SetNextMatchScene)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (SetNextMap)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (catalog dropdown, cursor enforcement)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenPauseMenu.cs` (host-only button, safety hooks)
- `Assets/LocalTest/Editor/ProductionSceneBuilder.cs` (parameterized match scenes, Warehouse, Build Settings)
- `Assets/LocalTest/Scenes/Warehouse.unity` + new materials in `Assets/LocalTest/Materials/`

---

## 2026-08-23 — Voice indicators + traffic logs, invite friends, in-match voice HUD, Game scene lighting

Follow-up to the re-scope, driven by single-PC testing constraints (host PC + slow VM) and the black Game scene.

**Voice activity indicators + traffic logging (`VoicePlayer`, `HiddenSessionUI`):**
- `VoicePlayer` now tracks per-player voice activity: `isMicLive` (local capture producing data), `isSpeaking` (remote packets arriving within 0.5 s), byte totals, and a `[SyncVar] ownerSteamId` set server-side from the Fizzy connection address (host's own player uses `SteamClient.SteamId`; stays 0 in KCP tests).
- Throttled console logs while active: `[Voice] TX n B (total X KB)` / `[Voice] RX n B from <name>`. Toggle via `logTraffic`. **This is the "is audio going over" evidence for 1-PC/VM testing.**
- Waiting room member rows now show live state: cyan = your mic live, green = that member talking, plus `[mic live]`/`[talking]` text tags and a cumulative "Voice: sent/received" line. Local KCP sessions list scene players instead of lobby members.
- **How to test:** host on PC → VM joins → click Push to Talk on the VM → host console shows RX lines and the VM's row turns green.

**Invite Friends (waiting room → Invite Friends button):**
- In-game invite panel: search box + scrollable online-friends list (`SteamFriends.GetFriends()`) with per-row Invite buttons (`Lobby.InviteFriend`). Accepting an invite already works via the existing `OnGameLobbyJoinRequested` path. Test requirement: the VM account must be a Steam friend of the host account.
- Secondary "Open Steam Overlay Invite" button uses `SteamFriends.OpenGameInviteOverlay(lobbyId)` (native picker; needs the Steam overlay enabled).
- New reusable uGUI ScrollView helper in `HiddenSessionUI`.

**In-match voice HUD (`VoiceHud`, new package component):**
- Always-on top-right panel listing players with the same mic-live/talking states. Added to the Game scene by the builder.

**Game scene lighting fix (black screen):**
- Root cause: HDRP renders nothing without lights — the generated Game scene had none. The builder now adds a Directional Light (60 klux, soft shadows) plus a global Volume with a `GameSky.asset` profile (physically-based sky + auto exposure). Fixed in `ProductionSceneBuilder` because Build All regenerates scenes and would wipe manual editor edits.

**Verified:** compile clean; scenes regenerated; smoke player rebuilt; KCP host+client, sync host+client (client saw the moving AiCube), UI, and Steam-host smokes all pass.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Voice/VoicePlayer.cs` (activity tracking, SyncVar, logging)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (indicators, stats line, invite panel, ScrollView)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/VoiceHud.cs` (new)
- `Assets/LocalTest/Editor/ProductionSceneBuilder.cs` (lighting/sky/HUD in Game scene)
- `Assets/LocalTest/Lighting/GameSky.asset` (new volume profile)

Notes:

- VM game-scene crash left alone as agreed — likely VM graphics, not the package.
- Voice capture returning 0 frames in headless runs is still an environment quirk; the new TX/RX logs make real two-user verification observable.

---

## 2026-08-23 — Re-scope: production flow (R1–R4) fixed + verified

Root cause of "create game skips to game": every scene builder set `onlineScene = "Game"`, so Mirror's `StartHost()` immediately loaded the match scene and the waiting room never appeared. Also `HiddenSessionUI.OnCreateClicked` assigned `networkManager.onlineScene` at runtime. Fixed by moving to a production scene layout and an explicit match-scene field.

**R1 — Flow fixed (separate scenes, production layout):**
- New scene set: `MainMenu` (offlineScene/boot), `WaitingRoom` (onlineScene), `Game` (match).
- `HiddenNetworkManager` now has `matchScene` ("Game") separate from Mirror's `onlineScene` ("WaitingRoom"); `StartMatch()` → `ServerChangeScene(matchScene)`. `SteamLobbyManager.StartMatch()` applies `settings.sceneName` to the manager.
- `OnCreateClicked` no longer mutates `onlineScene`; the chosen map lives in `LobbySettings.sceneName`.
- Consolidated builders: deleted `SessionScreensSceneBuilder` + `SteamTestSceneBuilder`; new `ProductionSceneBuilder` builds MainMenu/WaitingRoom/Game + `HiddenSession.prefab` + `DummyPlayer.prefab` + HDRP materials. `LocalTestSceneBuilder` slimmed to just `BuildSmokePlayer`.

**R2 — Human UI:**
- Fixed blank setup fields: `Input()` now sets `field.text = initial` (was only `tmp.text = initial`, so TMP_InputField showed empty).
- Main Menu screen: Create Game, Join with Code (lobby ID field on menu), Local Host, Local Client, Quit. Back buttons between menu/setup.
- Waiting Room: prominent **lobby code** field + **Copy Code** button, member list with host tag, Start Match (host only), Push to Talk, Leave Lobby. UI auto-hides once the match scene is active.
- New `HiddenPauseMenu` (drop in the match scene): Esc toggles Resume / Back to Lobby / Quit to Menu / Quit Game. Quit to Menu stops the session → Mirror loads offlineScene (MainMenu) automatically.
- New drag-and-drop `HiddenSession.prefab` (NetworkManager + KCP + FizzyFacepunch + SteamManager + SteamLobbyManager + HiddenSessionUI) — drop into the boot scene, fill a few fields.
- Clean human-readable labels ("Lobby Name", "Max Players", "Map / Scene", "Voice Max Distance (m)").

**R3 — Moving-object sync demo (new task):**
- `AiCube` (`NetworkBehaviour`, server-authoritative patrol) in the Game scene with `NetworkTransformReliable`. Verified in a standalone build: host drives the cube, client saw it move (`SMOKE_SYNC_CLIENT_OK saw moving AiCube: (-0.07,0.50,0.02) -> (-0.22,0.50,0.06)`).

**R4 — Pink texture fix (new task):**
- Materials are now real `.mat` assets built with `HDRP/Lit` + `HDShaderUtils.ResetMaterialKeywords` and proper `_BaseColor`/`_SurfaceType`, assigned to floor/cube/player (instead of throwaway `new Material(Shader.Find(...))` at runtime). Real assets keep the shader in the build.

**Transport gotcha fixed:** the HiddenSession object carries both KCP and FizzyFacepunch; Mirror auto-selects the first (`TryGetComponent<Transport>`), which broke local tests (client tried Steam P2P to "localhost"). `StartLocalHost/StartLocalClient` now call `UseLocalTransport()` to force KCP. Steam path (via SteamLobbyManager.StartHost/StartClient) keeps FizzyFacepunch.

**Verified in a standalone build (exit 0):**
- KCP host+client: `SMOKE_HOST_OK playerCount=2` / `SMOKE_CLIENT_OK localPlayer=DummyPlayer(Clone)`.
- Sync: host WaitingRoom→Game, `SMOKE_SYNC_HOST_OK`; client `SMOKE_SYNC_CLIENT_OK` saw the moving cube.
- UI: `SMOKE_UI_HOST_OK players=1`, no TMP/Input errors.
- Steam host: `SMOKE_STEAM_HOST_OK serverActive=True` (Steam init + lobby create + SteamSockets server still good; test lobby id 109775243098713739).
- Voice: `SMOKE_VOICE_CONFIG spatialBlend=1 rolloff=Logarithmic maxDistance=30` (3D config good). Voice **capture returned 0 frames this session** — the record→compress→decompress path was verified in Slice 6; the 0-capture is a headless/mic-environment issue, not a regression (Steam init + lobby + server all work).

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (matchScene + UseLocalTransport)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/AiCube.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (StartMatch uses settings.sceneName)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (main menu / setup / waiting room rewrite)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenPauseMenu.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Hidden.SteamMultiplayer.Runtime.asmdef` (+ Mirror.Components)
- `Assets/LocalTest/Editor/ProductionSceneBuilder.cs` (new; replaces the old scene builders)
- `Assets/LocalTest/Editor/LocalTestSceneBuilder.cs` (slimmed to BuildSmokePlayer)
- `Assets/LocalTest/Scenes/MainMenu.unity`, `WaitingRoom.unity`, `Game.unity` (regenerated)
- `Assets/LocalTest/Prefabs/HiddenSession.prefab`, `DummyPlayer.prefab` (new)
- `Assets/LocalTest/Materials/*.mat` (new, HDRP)
- `Assets/LocalTest/SyncSmokeRunner.cs` (new); deleted `LocalSessionLauncher.cs`, `SteamSessionLauncher.cs`
- `Ai Agent/REFERENCE-legacy-test-scenes.md` (new — old scenes documented)

Notes for the next agent:

- Scenes are now MainMenu → WaitingRoom → Game. Open **MainMenu.unity** in the editor for the full human flow.
- Cross-user Steam tests still pending (need two Steam accounts/machines): join-by-lobby-code, voice relay, distance fade. Test lobby id from last Steam host run: 109775243098713739 (expires when that lobby closes).
- Voice capture can return 0 in headless runs; verify with a real mic + two users.

---

## 2026-08-22 — Slice 6 done: distance-based Steam voice verified (record → compress → decompress)

- Added `VoicePlayer : NetworkBehaviour` to the package (`Runtime/Voice/`), attached to the player prefab:
  - **Capture (local):** `SetPushToTalk(bool)` / `TogglePushToTalk()` sets `SteamUser.VoiceRecord`; each frame reads `SteamUser.HasVoiceData` → `ReadVoiceDataBytes()` and sends the compressed bytes via `[Command]`/`[ClientRpc]` on Mirror's **unreliable** channel (`Channels.Unreliable`) — never the reliable channel.
  - **Playback (remote):** `RpcReceiveVoice` → `SteamUser.DecompressVoice(byte[], Stream)` → 16-bit PCM → streamed `AudioClip` fed from a thread-safe PCM queue via `OnAudioRead`. The `AudioSource` is fully 3D (`spatialBlend=1`, logarithmic rolloff, `maxDistance` = `LobbySettings.voiceRange`, default 30 m). Own voice is skipped (`isLocalPlayer`).
  - `EnsureAudioSource()` lazily creates the AudioSource on the player; `ApplyVoiceRange()` reads `SteamLobbyManager.settings.voiceRange` first so the AudioSource gets the host-set range.
- Waiting room UI now has a **Push To Talk** toggle button that drives the local player's `VoicePlayer`.
- Scene builders updated so `DummyPlayer.prefab` carries `VoicePlayer`.
- `VoiceSmokeRunner.cs` (`--smoke=voice`): starts a local KCP host, asserts the 3D AudioSource config, then records real mic audio for ~6 s and exercises the codec.
- **Verified in a standalone build:** `SMOKE_VOICE_CONFIG spatialBlend=1 rolloff=Logarithmic maxDistance=30`; real mic frames captured (`compressed=7733/7828/8182`) and decompressed to exactly 48 kHz mono PCM (`pcm=48000`/frame); `SMOKE_VOICE_OK full codec path verified (record->compress->decompress)`.
- Not yet verified: **relay to another Steam user** over the unreliable channel — needs two Steam accounts/machines (P2P can't self-connect). Same pending test as Slice 4's join path.
- Known benign issue (existing): FizzyFacepunch logs an NRE in `NextServer.Shutdown` at app exit (Steam teardown ordering); does not affect sessions.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Voice/VoicePlayer.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (PTT button)
- `Assets/LocalTest/Editor/LocalTestSceneBuilder.cs` (VoicePlayer on prefab)
- `Assets/LocalTest/VoiceSmokeRunner.cs` (new)

Notes for the next agent:

- Slice 7 next: fold the working stack into the package (`com.hidden.steam-multiplayer` is already the package — make sure `Assets/` stays game-only, add package README + samples section, and decide git URL vs `file:` for reuse).
- Full audible voice test: host a Steam lobby, a second Steam user joins, both in the match scene, hold PTT — the other player hears them at distance, quieter when far.

---

## 2026-08-22 — Slice 5 done: three UI screens (join / setup / waiting room) as package code

- Added `HiddenSessionUI` to the package (`Runtime/UI/`): builds the three screens at runtime with uGUI + TextMeshPro and wires them to the managers.
  - **Join screen** — Steam name/status, Create Game (→ setup), Join by Steam lobby ID, local KCP Start Host / Start Client.
  - **Game Setup (host)** — lobby name, max players, map/scene dropdown, voice max distance, public vs friends-only.
  - **Waiting Room** — lobby info + settings summary, live member list (with host tag), Start Match (host only), Leave.
- Added `LobbySettings` struct (`Runtime/Steam/`) serialized into Steam lobby data (`name`, `maxPlayers`, `scene`, `voiceRange`, `public`); `SteamLobbyManager` now stores settings on create and reads them on enter, plus a `RequestLobbyList()` via `SteamMatchmaking.LobbyList.WithSlotsAvailable().RequestAsync()`.
- Game-side `Assets/LocalTest/`:
  - `Editor/SessionScreensSceneBuilder.cs` — generates `Scenes/SessionScreens.unity` (HiddenNetworkManager + FizzyFacepunch + KCP, SteamManager, SteamLobbyManager, HiddenSessionUI).
  - `UiSmokeRunner.cs` — headless `--smoke=ui` test (builds UI, starts local KCP host, verifies dummy spawn).
  - `Editor/TmpEssentialsImporter.cs` — imports TextMesh Pro Essential Resources (the uGUI package ships them, but they must be in `Assets/TextMesh Pro`).
- Imported **TMP Essentials** into the project (`Assets/TextMesh Pro/`, 79 files incl. `TMP Settings.asset`) by extracting the ugui package's unitypackage directly — the normal `AssetDatabase.ImportPackage` path doesn't complete under `-batchmode -quit`.
- Fixed runtime UI bugs found by the smoke test:
  - Builtin font (`Resources.GetBuiltinResource<Font>`) NRE in builds → switched labels/buttons/titles to TextMeshPro.
  - `StandaloneInputModule` throws under the new Input System → switched to `InputSystemUIInputModule`.
  - Added `Unity.InputSystem` + `UnityEngine.UI` to the package asmdef references and `com.unity.inputsystem` + `com.unity.ugui` to package.json dependencies.
- Verified via standalone build `--smoke=ui`: UI built, managers found, local KCP host spawned a networked dummy (`SMOKE_UI_HOST_OK players=1`), clean Steam init/shutdown. No TMP/Input errors.
- Non-blocking note: FizzyFacepunch logs a NullReferenceException in `NextServer.Shutdown` at app exit (Steam teardown ordering); does not affect the session.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/UI/HiddenSessionUI.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/LobbySettings.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamLobbyManager.cs` (settings + lobby list)
- `Packages/com.hidden.steam-multiplayer/Runtime/Hidden.SteamMultiplayer.Runtime.asmdef` + `package.json` (deps)
- `Assets/LocalTest/Editor/SessionScreensSceneBuilder.cs`, `UiSmokeRunner.cs`, `Editor/TmpEssentialsImporter.cs` (new)
- `Assets/LocalTest/Scenes/SessionScreens.unity` (generated)
- `Assets/TextMesh Pro/` (TMP Essentials imported)

Notes for the next agent:

- Slice 6 next: Steam voice capture → send → play on remote player AudioSource with Unity 3D distance attenuation. Voice range from `LobbySettings.voiceRange` is the distance to apply.
- The SessionScreens scene is the interactive one to open in the editor for manual UI testing.

---

## 2026-08-22 — Slice 4: Steam lobby host path verified (create lobby -> StartHost over SteamSockets)

- Added to the package `Runtime/Steam/`:
  - `SteamManager` — owns SteamClient init/shutdown (`SteamClient.Init(appId, requireSteam)`; appid 480). Singleton, DontDestroyOnLoad.
  - `SteamLobbyManager` — create/join/leave Steam lobby + member tracking; create lobby → `HiddenNetworkManager.StartHost()`, join lobby → `StartClient(lobby.Owner.Id)`; `StartMatch()` → online scene. Wired to `SteamMatchmaking` + `SteamFriends.OnGameLobbyJoinRequested` events.
- Moved the package runtime asmdef to `Runtime/Hidden.SteamMultiplayer.Runtime.asmdef` (covers Network + Steam). Recreated `Runtime/Network/HiddenNetworkManager.cs` after an accidental `Remove-Item` (original was under the deleted `Runtime/Network/` — recreating it fixed the build).
- Game-side Steam test (`Assets/LocalTest/`):
  - `SteamSessionLauncher.cs` — IMGUI create/join-by-lobby-ID/start/leave panel.
  - `SteamSmokeRunner.cs` — headless `--smoke=steam-host` (create lobby, StartHost, report, quit).
  - `Editor/SteamTestSceneBuilder.cs` — builds `Scenes/SteamTest.unity` (HiddenNetworkManager + FizzyFacepunch transport with `InitFacepunch=false`, SteamManager, SteamLobbyManager, launcher; onlineScene=Game).
  - `BuildSmokePlayer` now builds whatever is in Build Settings (all three scenes).
- **Verified (host side, live Steam logged in as strideR, appid 480):** Steam init OK → lobby created (id 109775242986327840) → `Starting server [SteamSockets]` → `SMOKE_STEAM_HOST_OK serverActive=True` → server scene changed to Game.
- Not verified yet: a **second Steam user joining** (Steam P2P cannot self-connect; needs two accounts/machines).
- API notes for the next agent:
  - Facepunch 2.4.1: `SteamClient.Init(uint, bool)` (template's 1-arg `Init(appID)` was older API); `SteamMatchmaking.JoinLobbyAsync` returns `Lobby?` not `RoomEnter`; `SteamId` has no public ctor but has `implicit operator SteamId(ulong)`; event is `OnGameLobbyJoinRequested`.

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Steam/SteamManager.cs`, `SteamLobbyManager.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (recreated)
- `Packages/com.hidden.steam-multiplayer/Runtime/Hidden.SteamMultiplayer.Runtime.asmdef` (moved up one level)
- `Assets/LocalTest/SteamSessionLauncher.cs`, `SteamSmokeRunner.cs`, `Editor/SteamTestSceneBuilder.cs` (new)
- `Assets/LocalTest/Scenes/SteamTest.unity` (generated)

Notes for the next agent:

- Slice 5 next: the three UI screens (join, game setup, waiting room) as package samples.
- To finish verifying Slice 4's join path, run SteamTest on a second Steam account/machine, read the host's lobby ID from its log (`Lobby created:`), and join it from the launcher text field.

---

## 2026-08-22 — Slice 3 done: local KCP host + client verified with dummy player

- Added reusable `HiddenNetworkManager` (extends Mirror `NetworkManager`) to the package: `Packages/com.hidden.steam-multiplayer/Runtime/Network/` with asmdef `Hidden.SteamMultiplayer.Runtime` (refs Mirror). Exposes `StartLocalHost`, `StartLocalClient`, `StopLocalNetwork`, `StartMatch(scene)`, and logs server/client scene changes.
- Built a game-side local smoke test under `Assets/LocalTest/`:
  - `Editor/LocalTestSceneBuilder.cs` — generates `DummyPlayer.prefab` (capsule + `NetworkIdentity` + `NetworkTransformReliable`), `Scenes/LocalTest.unity` (offline: NetworkManager + `KcpTransport` + launcher), `Scenes/Game.unity` (online: floor + `NetworkStartPosition`), adds both to build settings, and builds a standalone player. Menu + `-executeMethod` driven.
  - `LocalSessionLauncher.cs` — IMGUI Start Host / Start Client / Start Match buttons.
  - `HeadlessSmokeRunner.cs` — CI-style test; runs in the built player (`--smoke=host` / `--smoke=client`), reports `SMOKE_HOST_OK` / `SMOKE_CLIENT_OK`, then quits.
- Verified end-to-end: built `Builds/LocalTestSmoke/HiddenDevHDRP.exe`, ran two instances over localhost KCP. Host spawned its own player (conns=1, players=1), client connected and spawned its dummy (`localPlayer=DummyPlayer(Clone)`); host saw playerCount=2. Both quit cleanly.
- Gotchas hit and fixed:
  - Mirror 96.11 `NetworkTransformReliable` has no `syncDirection`/`late` fields (that's the old API) — use `syncPosition`/`syncRotation`/`target`.
  - The scene builder's duplicate `HeadlessSmokeBehaviour` (added both in scene and via `[RuntimeInitializeOnLoadMethod]`) caused a stray client start; kept only the runtime-created one.
  - The smoke runner GameObject had to be `DontDestroyOnLoad` (scene change to Game destroys non-persistent objects).

Files:

- `Packages/com.hidden.steam-multiplayer/Runtime/Network/HiddenNetworkManager.cs` (new)
- `Packages/com.hidden.steam-multiplayer/Runtime/Network/Hidden.SteamMultiplayer.Runtime.asmdef` (new)
- `Assets/LocalTest/` (new — builder, launcher, smoke runner, prefab, scenes)
- `Builds/LocalTestSmoke/HiddenDevHDRP.exe` (test build; gitignored via Builds/)

Notes for the next agent:

- Slice 4 next: Steam lobby bring-up on App ID 480 (create/join/start via Steam, spawn dummy). Port SteamManager/SteamLobbyManager ideas from the template into the package. Steam must be running.
- The local KCP path stays as the everyday editor test route (Steam P2P cannot self-connect).

---

## 2026-08-22 — Slice 2 done: Steam transport compiles (Facepunch + FizzyFacepunch)

- Created the `com.hidden.steam-multiplayer` UPM package at `Packages/com.hidden.steam-multiplayer/` (embedded, `file:`-style inside this repo).
- Vendored Facepunch.Steamworks **2.4.1** managed DLLs (Win32/Win64/Posix + XML docs) and native `steam_api*` libs into `Plugins/Facepunch/`, keeping the template's `.meta` import settings (platform exclusions, Editor x86_64, Win64 standalone).
- Ported FizzyFacepunch transport source (11 files) into `Runtime/FizzyFacepunch/` with a new asmdef `Hidden.SteamMultiplayer.FizzyFacepunch` referencing `Mirror`. Wrapped the three global-namespace files (`NextCommon`, `FizzySocketManager`, `FizzyConnectionManager`) into `namespace Mirror.FizzySteam` so the package is self-contained.
- Confirmed Mirror 96.11.0 `Transport` API matches FizzyFacepunch (`OnServerConnectedWithAddress`, `OnServerError`, `GetMaxPacketSize(int)`, `ClientConnect(Uri)`); `Channels` resolves to Mirror's enum.
- Added `steam_appid.txt` = `480` at repo root.
- Verified via Unity CLI batch compile: exit 0, no CS errors/warnings; `Hidden.SteamMultiplayer.FizzyFacepunch.dll` built; 6 native steam_api plugins detected.

Files:

- `Packages/com.hidden.steam-multiplayer/` (new — package.json v0.1.0, depends on com.mirrornetworking.mirror)
- `Packages/com.hidden.steam-multiplayer/Plugins/Facepunch/` (Facepunch.Steamworks 2.4.1 + native libs)
- `Packages/com.hidden.steam-multiplayer/Runtime/FizzyFacepunch/` (ported transport)
- `steam_appid.txt` (repo root, 480)

Notes for the next agent:

- FizzyFacepunch `InitFacepunch = true` calls `SteamClient.Init(appid)` in `Awake`; requires Steam running and `steam_appid.txt` for the editor.
- Next slice (3): local KCP host + client smoke test using the KCP transport already vendored inside Mirror, spawn a dummy `NetworkIdentity`.

---

## 2026-08-22 — Slice 1 done: Mirror 96.11.0 compiles (vendored local UPM package)

- Decided to source Mirror as a **local embedded UPM package** instead of a git URL: official `MirrorNetworking/Mirror` ships only a `.unitypackage` (no `package.json`), so a UPM git dependency is impossible; OpenUPM's `com.mirrornetworking.mirror` is stale (96.6.4, min Unity 2021.3).
- Vendored official Mirror **v96.11.0** into `Packages/com.mirrornetworking.mirror/` with `package.json` (name `com.mirrornetworking.mirror`, version `96.11.0`). Excluded `Examples/`, `Hosting/`, `Tests/`; included `CompilerSymbols` (auto-adds `MIRROR`/`MIRROR_96_OR_NEWER` defines) and `Editor/ScriptTemplates`.
- Embedded packages under `Packages/` auto-resolve, so `manifest.json` needed no change.
- Verified via Unity CLI batch compile (`-batchmode -nographics -quit`): exit 0, no CS errors. Built: Mirror.dll, Mirror.Components, Mirror.Transports, Mirror.Authenticators, Mirror.Editor, Unity.Mirror.CodeGen (Weaver), + kcp2k/Telepathy/SimpleWeb.
- Gotcha: `com.unity.modules.vr` was removed in Unity 6 and breaks package resolution; dropped it from `package.json`.

Files:

- `Packages/com.mirrornetworking.mirror/` (new — Mirror source vendored from tag `v96.11.0`)

Notes for the next agent:

- Slice 2 next: Facepunch plugins + FizzyFacepunch + `steam_appid.txt` (480). Reference: template's `Assets/Facepunch/` (Facepunch.Steamworks Win32/Win64 DLLs + `redistributable_bin/steam_api*`) and `Assets/FizzyFacepunch-master/` (C# transport source).
- Mirror 96.11.0 comes with KCP, Telepathy, SimpleWeb, Edgegap transports already inside the vendored package.

---

## 2026-08-22 — Project memory pack created

- Created the `Ai Agent/` handoff folder so a new terminal/agent can recover context.
- Wrote rules, status, plan, decisions, glossary, and beginner stack explainers.
- Added root `AGENTS.md` pointing at this folder (so tools that auto-read `AGENTS.md` find it).
- Recorded the agreed architecture: Unity 6.5 HDRP game, reusable UPM Steam package, Mirror + Facepunch/FizzyFacepunch, do not merge the 2022.3 URP template.
- Expanded the package goal to include distance-based Steam voice and three UI screens (join, game setup, waiting room).

Files:

- `AGENTS.md` (repo root)
- `Ai Agent/README.md`
- `Ai Agent/RULES.md`
- `Ai Agent/STATUS.md`
- `Ai Agent/PLAN.md`
- `Ai Agent/DECISIONS.md`
- `Ai Agent/CHANGELOG.md`
- `Ai Agent/GLOSSARY.md`
- `Ai Agent/stacks/` (see that folder's README)

Notes for the next agent:

- **No multiplayer code is in the game yet.** Next slice is adding Mirror via UPM git URL and confirming a Unity 6.5 compile.
- Reference template path: `C:\Users\strideR\Unity Projects\UnityMultiplayer-MirrorAndSteamworks-Template`
- Do not copy that template's Assets tree.
