# Issues & known problems — Imasu Steamworks

Known problems and open issues for the steamworks core + Control Center. Fixed items move to `CHANGELOG.md`; still-open items stay here.

## Open

- **Cross-user Steam paths unverified** (need a second Steam account/machine; Steam P2P can't self-connect): join lobby by code → StartClient toward owner; two-way voice relay / distance fade between real players.
- **Player smokes unrun in this pass** — the smoke player built clean but couldn't init graphics in the headless shell. `--smoke=ui`, `--smoke=lan-flow`, `--smoke=dedicated` need a display/real machine (Phase 5 gate).
- **Mirror is vendored** (embedded package) — updating Mirror means re-vendoring a new tag, not bumping a version string (D4). Consumers get Mirror via the Control Center **"Install Steam dependencies"** bootstrap (git-path shim — Mirror's own repo has no UPM package.json).
- **Consumer install now proven headlessly (2026-08-31):** the git-URL path (core + LAN + voice + Mirror + Facepunch shims) compiled with zero errors in a throwaway project. Remaining: GUI click-through of the Control Center bootstrap on a display machine. `versionDefines` were found NOT to fire for git-installed packages — addon `defineConstraints`/core `versionDefines` removed ("installed = active").

## Resolved (recent, for context)

- **Publish unexercised** — fixed 2026-08-30: `release/<pkg>@0.1.0` tags for all four packages ran the workflow (4 green), created scoped tags + `.tgz` Releases; consumer git-URL verified via clone. Caveat: tags pushed before GitHub registers a new workflow are dropped — re-push.
- **Addon Manager UX gaps** — fixed 2026-08-30: `catalog.json` (not hardcoded), update-available semver check, filter, Release-notes links, surfaced poll errors.
- **Runtime `IImasuModule` scaffolded but unwired** — fixed 2026-08-30: `ImasuSteamworksMaster.DiscoverModules()` (AppDomain reflection, cached) validates enabled modules on every `ApplyToComponents`; Voice/LAN/Dedicated each ship a runtime implementation.
- **Control Center lag** — fixed 2026-08-30: per-repaint full-project `FindAssets("t:SceneAsset")` scans and scene/package scans removed behind caches (`SceneCache`, 1 s spawn cache, cached master check, cached package list). Contract codified as D24.
- **Scene whitelist** — fixed 2026-08-30: `Validate Maps` button (Scenes & Lobby + Gamemodes tabs) opens each catalog scene additively, checks for a Session Manager + spawn, caches results, flags failures. Bottom bar now shows a live "X/Y valid" summary.
- **Host map list stale** — fixed 2026-08-30: `CatalogVersion` + `ImasuSessionUI` rebuild + `Sync Session Prefab to Config` re-bakes the prefab catalog.
- **Spawn at 0,0,0 for teamId=1** — root-caused 2026-08-30 (spawns with teamId>0 are exclusive to that team; no team assignment exists yet). Documented in the Spawns tab; keep spawns on Any (0).
- **HDRP map slow to load** — expected (1.2 MB baked lighting vs ~30 KB procedural maps); user chose to leave it.
- **Typing lag in Detailed Config** — fixed: Delayed fields + guarded `ApplyModifiedProperties` (commit on Enter/focus-lost).
- **Build Profiles vs Build Settings** — fixed: sync is additive + mirrors the active Unity 6 Build Profile.