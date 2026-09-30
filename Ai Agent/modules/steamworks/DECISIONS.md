# Decisions — Imasu Steamworks

Locked choices for the `com.imasu.steamworks` package and its addons. Do not reopen unless the user asks, or a slice proves the choice is blocked.

Project-wide decisions (engine, repo, module identity, addon architecture) live in the root `Ai Agent/DECISIONS.md`. Voice-specific decision D8 lives in `Ai Agent/modules/voice/DECISIONS.md`.

When a decision changes: update this file, add a changelog entry, and fix STATUS.md if needed.

## D4 — Mirror, not Unity Netcode for GameObjects, not FishNet (for now)

**Choice:** Mirror 96.11.0, vendored as a **local embedded UPM package** (`Packages/com.mirrornetworking.mirror/`), not the vendored 96.0.6 tree from the template.

**Why:** The template already has a working Mirror + Steam lobby pattern. Hidden Source is a small listen-server game. NGO would throw away that work. FishNet is fine but is a rewrite we do not need.

**Sourcing note (2026-08-22):** Official `MirrorNetworking/Mirror` ships only a `.unitypackage` (no `package.json`), so a UPM git dependency is not possible. OpenUPM's `com.mirrornetworking.mirror` is stale (96.6.4). Decision: vendor the official v96.11.0 source as an embedded package under `Packages/`, excluding Examples/Hosting/Tests. Update by re-vendoring the newer tag.

## D5 — Facepunch + FizzyFacepunch first

**Choice:** Keep Facepunch.Steamworks and FizzyFacepunch. Fallback is Steamworks.NET + FizzySteamworks if Unity 6.5 will not run Facepunch.

**Why:** Lobby scripts already use Facepunch's C# events (`SteamMatchmaking.OnLobbyCreated`, `SteamClient.Init`). Porting ~8 files is cheaper than switching APIs up front.

**Init ownership (2026-08-22):** `SteamManager` owns `SteamClient.Init(480, true)` + shutdown. The FizzyFacepunch transport is set with `InitFacepunch = false` in the Steam test scene so there is a single init point. Facepunch 2.4.1 verified: init, lobby create, SteamSockets server start all work on 6000.5.9f1.

## D6 — Steam Relay P2P listen-server, not dedicated server (v1)

**Choice:** Lobby owner is the host. Clients connect by Steam ID through Steam Networking Sockets / Relay.

**Why:** Matches Hidden Source-style small matches and the template. No server bill. NAT is Steam's problem.

## D7 — Local KCP path is required

**Choice:** NetworkManager can swap to KCP/Telepathy for editor/localhost tests.

**Why:** Steam P2P cannot connect to yourself. Without KCP, every test needs two Steam accounts.

## D9 — Three screens are part of the package

**Choice:** Join, game setup, and waiting room ship as package runtime code (`HiddenSessionUI`, builds uGUI at runtime) plus a `SessionScreens` sample scene, not as one-off game scenes buried in SampleScene.

**Why:** The package goal is a working Steam game session, not just a transport. Future projects should get the flow, not only DLLs. Building the UI in code (rather than a hand-authored scene) keeps it version-safe and reusable; the sample scene wires the components and auto-finds managers.

**Dependency note (2026-08-22):** The UI uses TextMeshPro (`Unity.TextMeshPro`) and the new Input System (`Unity.InputSystem`); both are declared as package dependencies. TMP **Essentials** (the `Assets/TextMesh Pro/Resources/TMP Settings.asset` resource) must exist in the host project or runtime TMP text NREs — import via `Hidden.LocalTest.Editor.TmpEssentialsImporter`.

## D10 — Gameplay waits

**Choice:** No Hidden vs IRIS, invisibility, or weapons until slices 1–7 in `modules/steamworks/PLAN.md` work.

**Why:** Networking and voice on a dummy player is the risky, reusable layer. Gameplay on a broken session is wasted work.

## D11 — Dev Steam App ID is 480

**Choice:** Spacewar (480) until a real App ID exists.

**Why:** Standard Steamworks bring-up. Replace before any public or store build.

## D12 — Package is embedded under Packages/, not a sibling folder

**Choice:** The Steamworks package lives at `Packages/com.imasu.steamworks/` inside this repo (embedded UPM package), rather than as a sibling folder referenced by `file:` path.

**Why:** Keeps everything in one repo, auto-resolves in Unity, and is trivial to publish as a git URL later. Development builds directly inside the package.

## D13 — FizzyFacepunch transport, ported and namespaced

**Choice:** Port FizzyFacepunch (11 source files, Facepunch 2.4.1) into `Runtime/FizzyFacepunch/` with a dedicated asmdef referencing Mirror; wrapped the three global-namespace files into `Mirror.FizzySteam`.

**Why:** The template's transport compiled against Mirror 96.0.6; our vendored 96.11.0 has the same `Transport` API, so the port is a straight copy + namespace cleanup. No Steamworks.NET/FizzySteamworks fallback needed yet.

## D14 — Production scene layout (MainMenu / WaitingRoom / Game), not panels in one scene

**Choice (2026-08-23):** Three separate scenes: `MainMenu` = offlineScene/boot, `WaitingRoom` = onlineScene, `Game` = match (loaded via `StartMatch`). `HiddenNetworkManager.matchScene` is separate from Mirror's `onlineScene`.

**Why:** The original builders set `onlineScene = "Game"`, so `StartHost()` skipped the waiting room. The production layout maps directly onto Mirror's offline/online model and gives "Back to Menu" for free (`StopHost`/`StopClient` → offlineScene). Panels-in-one-scene was simpler but wrong for production.

## D15 — Transport is chosen explicitly, not by component order

**Choice (2026-08-23):** The session object carries both KCP and FizzyFacepunch. `StartLocalHost`/`StartLocalClient` force the KCP transport (`UseLocalTransport`); the Steam path keeps FizzyFacepunch.

**Why:** Mirror auto-selects the first `Transport` on the object (`TryGetComponent`), which broke local tests (client tried Steam P2P to "localhost"). Explicit selection keeps both paths working on one object.

## D17 — One config asset + Control Center window (2026-08-24)

**Choice:** All tunables (ours and dependency fields: SteamManager, FizzyFacepunch, KcpTransport, mover) live in a single `ImasuSteamworksConfig` ScriptableObject edited from one `Imasu → Steamworks Control Center` window. `ImasuSteamworksMaster` pushes config → components at runtime. Voice: `useDistanceAttenuation` OFF = Fixed (no dropoff, per-user VOIP volume only); ON = Range (min/max rolloff). Settings menu lets players tune VOIP volume, not distance. `minPlayers` enforced (1 = host solo). `allowNonSteamServers` is a debug listing flag (hides LAN paths when off). Docs are offline static HTML in `Docs/` (Docusaurus port later).

**Why:** One source of truth is easier for new devs than six scripts; the window guides configuration and applies it. Voice distance is host-owned; per-user volume compensates for mic loudness. Static HTML docs open by double-click with no npm server.

## D18 — Kill-switch + gamemodes/scenes + ESC + avatar poll (2026-08-24)

**Choice:**
1. **Addon kill-switch = data, not component state.** `ImasuSteamworksConfig.addonEnabled` (top of Control Center). OFF suppresses the whole addon (no hard errors, no Steam init, sessions soft-blocked with a banner); the component's `enabled` stays a per-scene override. Hard gate: a master with NO config wired hard-blocks hosting/joining/match-start with a red banner. Rationale: one click propagates everywhere (scenes + prefab share the asset) and survives builds; Unity's per-object checkbox doesn't scale and kills the wrong thing if you untick the GameObject.
2. **Gamemodes now, simple.** `gameModes[]` (name + per-mode maps) replace the flat map list; `mainMenuScene`/`lobbyScene` route scenes (equal = shared). Host Setup = Gamemode → Map dropdowns. Map catalog is the single source for setup, browser, and in-match `ServerChangeMap`. Rationale: the user's future flow is "add a map/gamemode in the Control Center and it shows up in UI + supports mid-game switching" — groundwork now, no over-engineering (no per-gamemode rules yet).
3. **ESC closes modals only** (invite overlay, dropdown popups) — never scene navigation, per user call.
4. **Avatars: poll, don't retry on event.** The many "fill after typing" fixes (pool reuse, re-init on same-id, persona hook) still missed the first-open case because the download lands while rows are subscribed-but-null. `AvatarLateAssign` now polls the cache every 0.5s for 12s (plus the `onLoaded` handshake) and the bottom-up flip moved centrally. Rationale: polling is idempotent, cheap (cache lookup), and survives any event-ordering miss.

## D19 — Control Center 2.0: per-mode overrides, Session Manager, spawns, modular roadmap (2026-08-24)

**Choice:**
1. **Per-gamemode + per-map sparse overrides** (curated, bool-pair): map → gamemode → global. Steam id / scenes / kcpPort stay global-only. Effective values drive lobby creation, minPlayers, voice and mover.
2. **Session Manager prefab = required per-scene contract**, living in the package. Hybrid lifetime: per-scene prefab instances; only the first survives (DontDestroyOnLoad). Scene whitelist (a map must contain it) prevents "any scene added by accident."
3. **Spawns are physical + team-filtered**: SpawnPoint/SpawnArea + ImasuTeamConfig + SpawnManager (anti-clump, round-robin), wired into OnServerAddPlayer.
4. **Control Center = Gaia-style master hub** (tabbed, tutorial, per-module settings) + **PurrNet-Packages-style modular packages**: core only by default; Voice/LAN/Dedicated optional addons (LAN ≠ Dedicated). Monorepo scoped tags.

**Why:** User wants friendly one-window control + "add a map/gamemode in the Control Center and it appears everywhere", optional addons so core stays light, and a scene guard so users can't select a non-multiplayer scene. PurrNet Packages = the addon-loader model; Gaia = the window-manager model; we keep OUR Control Center as the surface.

## D21 — Active Config picker + auto-apply (2026-08-30)

**Choice:** The Control Center has a real **Active Config** `ObjectField` at the top; picking a different config asset persists the choice (`EditorPrefs` `ImasuSteamworks.ActiveConfigPath`), rebuilds the editor, and **auto-applies** to every Session Manager in the open scenes. "Select Config" (which only pinged the already-loaded default) and "Create / Select Config" are gone; explicit **Create New Config…** (fresh empty asset) + **Duplicate Template…** (copies the StarterConfig sample) replace them. Both existing config assets stay (`ImasuSteamworksConfig.asset` legacy + `ImasuSteamworksConfig_Hidden.asset` production — D17's single-asset model, two presets). User decisions: auto-apply = yes; keep both files; label = "Active Config".

**Why:** The old bar had no way to actually switch configs (no `ObjectField`), so "switch default → Hidden" was impossible. One field + auto-apply is the friendliest out-of-box behaviour.

## D22 — Control Center tabs rework + Build Profile sync + catalog staleness (2026-08-30)

**Choice:**
1. **Host Setup dropdown reads the live catalog, not a frozen snapshot.** `ImasuSteamworksMaster` exposes `CatalogVersion` (bumped in `ApplyToComponents`); `ImasuSessionUI` rebuilds the setup mode/map options once per catalog change (not every ShowSetup — user: "when gamemode is changed is ok"). A disk prefab can still be stale → new `ProductionSceneBuilder.SyncSessionPrefabFromConfig` (`Imasu/Steamworks/Sync Session Prefab to Config`) re-bakes it without touching scenes/materials.
2. **Build Settings = additive + Unity 6 Build Profile aware.** `SyncBuildSettings` and `AddToBuildSettings` merge (never delete) configured scenes into both classic `EditorBuildSettings.scenes` and the active `BuildProfile.scenes`. API verified on 6000.5: `BuildProfile.GetActiveBuildProfile()` + instance `scenes` property (`get_scenes`/`set_scenes`); the static `GetScenes`/`SetScenes` do NOT exist.
3. **Purpose tabs over the raw inspector (beginner-friendly).** Project Scenes & Lobby = scenes + Session defaults + Steam foldout; Gamemodes & Maps = editable catalog (add/reorder/remove, map scene picker, per-mode override toggles, live Effective line); Spawns & Teams = per-spawn team popup + inline `ImasuTeamConfig` editor (min separation, 1/2/4 presets, name/color/spawnTeamId). Detailed Config stays as the advanced fallback with a guarded `ApplyModifiedProperties` (commits on Enter/focus-lost → fixes per-char typing lag) + cached scene lookups.
4. **Spawn teamId semantics confirmed (no behaviour change):** `TryGetSpawn(0)` only matches `TeamId==0` (Any) providers; teamId=1 spawns are exclusive to team 1 and there is no player→team assignment yet, so a map with only teamId=1 spawns falls back to 0,0,0. Kept as-is; the Spawns tab now explains "keep spawns on Any (0)". Teams are labels/spawn labels, not runtime assignment.

**Why:** User reported maps not showing in Host, Build Profiles vs Build Settings confusion, sparse tabs, and a 10 s typing lag. The rework surfaces the most-used settings in their purpose tabs, keeps the raw inspector debounced, and makes "add a map → it appears everywhere" true with zero manual Build Profile steps.

## D24 — Control Center performance contract (2026-08-30)

**Choice:** The Control Center window repaints on every mouse move (IMGUI), so **no OnGUI frame may scan the project, the scene, or the package registry**. All lookups go through caches: `SceneCache` (one `FindAssets("t:SceneAsset")` pass, invalidated on `EditorApplication.projectChanged`), a 1 s spawn scan cache, a cached master-presence check, and a cached `GetAllRegisteredPackages` list. Validation ("scenes missing from Build Settings") is computed against the cache, not a 0.5 s timer. The kill-switch toggle no longer calls `AssetDatabase.SaveAssets()` (dirty assets autosave).

**Why:** The window stuttered twice a second even when idle (per-scene full-project `FindAssets` scans) and per-repaint on every tab. The contract prevents regressions as new tabs and the Addon Manager are added.