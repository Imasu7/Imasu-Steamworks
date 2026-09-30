# Decisions (project-wide index)

Locked choices that apply to the **whole project**. Choices that apply to one module live in that module's `DECISIONS.md`:

| Module | Decisions file |
|--------|----------------|
| Imasu Steamworks (+ Control Center) | [`modules/steamworks/DECISIONS.md`](modules/steamworks/DECISIONS.md) |
| Imasu Steamworks Voice | [`modules/voice/DECISIONS.md`](modules/voice/DECISIONS.md) |
| Imasu Steamworks LAN | [`modules/lan/DECISIONS.md`](modules/lan/DECISIONS.md) |
| Imasu Steamworks Dedicated Servers | [`modules/dedicated/DECISIONS.md`](modules/dedicated/DECISIONS.md) |
| Imasu Character Controller | [`modules/character/DECISIONS.md`](modules/character/DECISIONS.md) |

Rule: when a decision changes, update its file, add a changelog entry, and fix STATUS.md if needed.

## D1 — Stay on Unity 6.5 HDRP for the game

**Choice:** Hidden Dev HDRP (6000.5.9f1, HDRP) is the game project.

**Why:** That is the new project. Hidden Source-style lighting and atmosphere fit HDRP. We will not downgrade to 2022.3 to make the old template happy.

## D2 — Do not merge the Mirror/Steam template project

**Choice:** Treat `UnityMultiplayer-MirrorAndSteamworks-Template` as reference only. **Removed 2026-08-24** — all of its reusable pieces were ported (Mirror 96.11.0 vendored; Facepunch 2.4.1 + FizzyFacepunch in the package); no remaining dependency.

**Why:** Different Unity major, URP vs HDRP, and it vendors Mirror + examples + store assets into `Assets/`. Merging would contaminate a clean game.

## D3 — Reusable UPM packages, not a .unitypackage, not a third Unity project

**Choice:** Every Imasu module is a UPM package (`com.imasu.*`) developed inside this project as an embedded package, published later by git URL. A `.unitypackage` is the old, messier mechanism; a third blank Unity project would mean another Library folder and version drift.

**Why:** Same "drop into a new project" goal as an asset, but versioned, per-module, and not copied into `Assets/`.

## D16 — Modules are "Imasu", game-agnostic (2026-08-24)

**Choice:** All reusable packages are `com.imasu.*` / namespace `Imasu.*` / window "Imasu Steamworks Control Center". Nothing in a package is named `Hidden` — that name belongs to the game only (`Hidden Dev HDRP`, its scenes, its `Assets/` code). Prefab `ImasuSession`, manager `ImasuNetworkManager`, UI `ImasuSessionUI`, pause `ImasuPauseMenu`.

**Why:** The user wants the Unity packages reusable across projects, separate from the Hidden game. Rebrand preserved `.meta` GUIDs so scenes/prefabs kept working. C# consequence: the package namespace segment `Steamworks` collides with Facepunch's `Steamworks` namespace — Facepunch references are written `global::Steamworks.*`.

## D20 — Modular addon architecture (2026-08-24)

**Choice:** Core is the only default. Each optional addon is a separate UPM package (`com.imasu.steamworks.voice`, `.lan`, `.dedicated`, `com.imasu.character`, `.multiplayer`) with `defineConstraints` where needed. Core compiles WITHOUT the addons by depending on core-defined interfaces — `IVoiceMember`, `ILanDiscovery`+`LanServerInfo`+`LanDiscoveryQuery`, and future `IImasuModule` (Control Center tab registry) and `ICharacterNetworkAdapter`. No asmdef cycles, no hard references to addon types. Defines live in ProjectSettings for the dev project + `versionDefines` on the core asmdef for consumers. **LAN ≠ Dedicated** (never bundled). `kcp2k` stays a Mirror transitive dep; Facepunch DLLs stay vendored in the steamworks core (a `com.imasu.facepunch` wrapper is deferred — no runtime benefit).

**Why:** PurrNet-Packages-style optional modules (core only by default). The interface-boundary approach keeps the split clean without forcing core → addon references, which would create compile cycles.

## D23 — Modular docs + worktree-ready layout (2026-08-30)

**Choice:** The repo is laid out as **one module = one `Packages/com.imasu.*` folder + one `Ai Agent/modules/<name>/` docs folder**. Root `Ai Agent/` holds only project-wide docs (rules, status, changelog, overview, project-wide decisions, worktree contract). A module's planning (PLAN/ROADMAP/DECISIONS/ISSUES/TECH) is self-contained so it can be developed in its own ORCA worktree. Root `Docs/` (consumer-facing offline HTML) is organized by module too.

**Why:** The user asked for the project to be modular from both a code and documentation perspective before it gets hard to maintain, and for modules to be developable independently in worktrees without a mess of dependencies.