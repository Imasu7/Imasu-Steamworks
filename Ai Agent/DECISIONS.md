# Decisions (repo-wide)

Locked choices that apply to **every package in this repo**. Choices that apply to one package
live in that package's `Ai Agent/modules/<name>/DECISIONS.md`:

| Package | Decisions file |
|---|---|
| Imasu Steamworks core + Control Center | [`modules/steamworks/DECISIONS.md`](modules/steamworks/DECISIONS.md) |
| Imasu Steamworks Voice | [`modules/voice/DECISIONS.md`](modules/voice/DECISIONS.md) |
| Imasu Steamworks LAN | [`modules/lan/DECISIONS.md`](modules/lan/DECISIONS.md) |
| Imasu Steamworks Dedicated Servers | [`modules/dedicated/DECISIONS.md`](modules/dedicated/DECISIONS.md) |

D4, D5, D20 and D24 below are repo-wide and are also expanded in
`modules/steamworks/DECISIONS.md`, which keeps the implementation detail (exact APIs, file
paths, dates). If the two ever disagree, stop and reconcile — do not pick one silently.

Rule: when a decision changes, update its file, add a changelog entry, and fix `STATUS.md` if
needed.

---

## D3 — Reusable UPM packages, not a `.unitypackage`

**Choice:** Every module is a UPM package (`com.imasu.*`) in `Packages/`, versioned
independently and installable by git URL or path. A `.unitypackage` is the older, messier
mechanism that copies files into `Assets/` and is hard to update.

**Why:** Same "drop into a new project" goal as an asset, but versioned, per-package, and it
does not scatter into the consumer's `Assets/`.

## D4 — Mirror 96.11.0, vendored as an embedded UPM package

**Choice:** Mirror 96.11.0 (not Netcode for GameObjects, not FishNet), vendored as an embedded
UPM package at `Packages/com.mirrornetworking.mirror/`, with `Examples/`, `Hosting/` and
`Tests/` excluded. Updating means re-vendoring a newer upstream tag — never a hand-patch.

**Why:** Mirror is a working, understood networking model for a small listen-server game, and
switching netcode libraries would be a rewrite with no benefit. NGO/FishNet were rejected on
the same grounds.

**Sourcing note:** the official `MirrorNetworking/Mirror` repo ships only a `.unitypackage`
(no `package.json` at any tag), so a UPM git dependency is impossible. OpenUPM's
`com.mirrornetworking.mirror` is stale (96.6.4). Therefore the MIT v96.11.0 source is vendored
here and exposed to consumers as a free git-path shim.

## D5 — Facepunch + FizzyFacepunch first

**Choice:** Facepunch.Steamworks for the Steam API and FizzyFacepunch as the Mirror transport
over Steam Networking Sockets. Both ship in the free `com.imasu.facepunch` package. The
fallback (Steamworks.NET + FizzySteamworks) is only revisited if Facepunch fails to compile or
run on the target Unity version.

**Why:** The lobby layer is already written against Facepunch's C# events
(`SteamMatchmaking.OnLobbyCreated`, `SteamClient.Init`). Switching API up front would mean
porting every file for no proven gain.

**Init ownership (one init point):** `SteamManager` owns `SteamClient.Init` and shutdown. The
FizzyFacepunch transport is configured with `InitFacepunch = false` so it never re-initialises
Steam behind the manager's back.

## D16 — Packages are "Imasu" and game-agnostic

**Choice:** All packages are `com.imasu.*`, namespaces are `Imasu.*`, and the editor window is
"Imasu Steamworks Control Center". No package file, class, prefab, tooltip or config default is
named after a game. Type names are neutral: `ImasuSession`, `ImasuSessionManager`,
`ImasuNetworkManager`, `ImasuSessionUI`, `ImasuPauseMenu`. The rename that produced these names
preserved `.meta` GUIDs, so prefabs and scenes kept resolving.

**Why:** The packages are meant to drop into any Unity 6 project. A game name in the public API
is a permanent maintenance and credibility cost. C# consequence: the `Steamworks` namespace
segment collides with Facepunch's, so Facepunch references are written `global::Steamworks.*`.

## D20 — Modular addon architecture ("installed == active")

**Choice:** Core is the only default package. Each optional capability is a separate UPM
package (`com.imasu.steamworks.voice`, `.lan`, `.dedicated`) that implements interfaces
**defined in core**: `IVoiceMember`, `ILanDiscovery` + `LanServerInfo` + `LanDiscoveryQuery`,
`IImasuModule` (runtime module contract) and `IImasuModuleEditor` (Control Center tab
registry). Core compiles with zero references to any addon; addons may depend on core.

An addon is active because it is **installed** — there are no kill-switch defines and no
`defineConstraints` gating. (`versionDefines` and `defineConstraints` were removed after they
were found not to fire for git-installed packages, which silently skipped the addon
assemblies; the asmdefs now carry empty arrays.)

Runtime validation goes through `ImasuSteamworksMaster.DiscoverModules()`; each addon ships its
own `IImasuModule` implementation. LAN and Dedicated are **separate** addons and are never
bundled together.

**Why:** The interface boundary keeps the split clean without a compile cycle, and keeps core
small for consumers who only need lobbies and a transport. "Installed == active" removes a
whole class of "the addon is there but does nothing" support failures.

## D23 — One package = one folder + one docs folder

**Choice:** Each module is exactly one `Packages/<package>/` folder plus one
`Ai Agent/modules/<name>/` docs folder (PLAN, ROADMAP, DECISIONS, ISSUES, TECH). Root
`Ai Agent/` holds only repo-wide docs. Cross-module capability is added as an interface in the
dependee and implemented in the dependent, so a change to one package rarely touches another's
files.

**Why:** Keeps the repo navigable as it grows, and keeps per-package planning self-contained
instead of smeared across one giant plan.

## D24 — Control Center performance contract

**Choice:** The Control Center repaints on every mouse move (IMGUI), so **no `OnGUI` frame may
scan the project, the scene, or the package registry**. Every lookup goes through a cache:
`SceneCache` (one `AssetDatabase.FindAssets("t:SceneAsset")` pass, invalidated on
`EditorApplication.projectChanged`), a 1-second spawn scan cache, a cached master-presence
check, and a cached `GetAllRegisteredPackages` list invalidated on the `registeredPackages`
event. Scene validation runs against the cache. The kill-switch toggle marks assets dirty
without calling `SaveAssets` (dirty assets autosave).

**Why:** The window stuttered several times a second while idle, because of per-repaint
full-project scans. The contract is written down so new tabs and the addon manager do not
reintroduce it.

---

## D25 — One repository (monorepo), not one repo per package

**Choice:** All six packages live in this single repository. Each is still individually
versioned, tagged, published and installable — the monorepo does not change the package
granularity.

**Why:** Every addon depends on core, so they move together; one CI workflow (`publish.yml`)
covers them all; consumers get one git URL; and independent per-package versions are exactly
what the Unity Asset Store submission needs (a store submission ships a single folder, while
git consumers pick packages by path).

## D26 — Canonical source is this repo; the game repo keeps embedded copies

**Choice:** `Imasu7/Imasu-Steamworks` is the canonical source. The `Hidden Dev HDRP` game repo
keeps embedded copies of the packages under its own `Packages/` for development. A change
ships to consumers only once it is committed here and tagged.

**Why:** The game needs an offline-capable sandbox and fast editor iteration; the split is
about ownership and release cadence, not about forcing the game onto git dependencies. See
`Ai Agent/STATUS.md` for the resulting workflow cost.

## D27 — Two-stage tag scheme

**Choice:** The human pushes `release/<pkg>@<version>`. The workflow validates that
`Packages/<pkg>/package.json` version matches, then creates the consumer-facing scoped tag
`<pkg>@<version>` and drafts the Release at that tag.

**Why:** The validation step can only happen in CI, and scoped tags are what consumers pin in
a git URL — so the two concerns are separated: a human-pushable trigger, a machine-created
pin.

## D28 — Install URLs hardcode the repo, with no registry indirection

**Choice:** Install URLs are literal
`https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/<pkg>#<pkg>@<version>` strings.
There is no OpenUPM / private-registry indirection yet.

**Why:** Nothing is published to a registry, so an indirection layer would be machinery with
nothing behind it. The whole change surface today is a handful of strings: `catalog.json`,
`ImasuPackageManager.cs`, package `package.json` metadata, the addon runtime `InstallUrl`
constants, the package READMEs, and one docs page.

---

## Moved to the game repo

These decisions were about the consuming game, not about these packages. They now live with
the game (`Hidden Dev HDRP`) and are not repeated here:

- Game engine and render-pipeline choices (Unity version, HDRP).
- Game project identity, scene layout, and gameplay architecture (roles, rounds, characters).
- Game-side content, smoke harness, and its verification runs.