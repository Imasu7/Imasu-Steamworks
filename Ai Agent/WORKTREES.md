# Worktrees — ORCA ADE contract

**Last updated:** 2026-08-30

This project is designed so each Imasu module can be developed in its **own ORCA worktree** without creating a mess of dependencies. This file is the contract for doing that.

## Why worktrees work here

- One module = one `Packages/com.imasu.*/` folder + one `Ai Agent/modules/<name>/` docs folder. No module's code or docs touch another module's files.
- Modules depend on each other only through **public interfaces** (`IVoiceMember`, `ILanDiscovery`, future `IImasuModule`, `ICharacterNetworkAdapter`), so a worktree editing one module rarely needs to edit another.
- Scenes and prefabs are largely **code-generated** (`ProductionSceneBuilder`, `SyncSessionPrefabFromConfig`, `AddToBuildSettings`) — so a worktree can regenerate them instead of merging YAML, which is the usual Unity merge hotspot.

## The module boundary contract (non-negotiable)

1. **Addons never edit core** (`com.imasu.steamworks`) and never edit another addon. If you need a change in core, do it in the main worktree (or a dedicated core worktree), tag/commit, then rebase the addon worktree onto it.
2. **Cross-module features go through interfaces defined in the dependee.** New shared capability → add an interface to the core/controller package, implement it in the addon.
3. **No cross-module file edits.** A worktree may only touch: its own `Packages/com.imasu.*/`, its own `Ai Agent/modules/<name>/`, and (for the game worktree) `Assets/`, `ProjectSettings/`, `Docs/`.
4. **Manifest/ProjectSettings are main-worktree-owned.** Adding/removing a package changes `Packages/manifest.json`; enabling a define changes `ProjectSettings/`. Addons avoid needing ProjectSettings defines by relying on `versionDefines` on the core asmdef (which auto-define when the addon is present). Any manifest/ProjectSettings edit happens in the main worktree, not in a module worktree.

## Suggested worktree layout

| Worktree | Owns | May regenerate |
|----------|------|----------------|
| `main` (this repo) | Integration: `Assets/`, `ProjectSettings/`, `Docs/`, all packages at rest | Everything — this is where scenes/prefabs get built and tested |
| `worktrees/steamworks` | `Packages/com.imasu.steamworks/`, `Ai Agent/modules/steamworks/` | `Assets/LocalTest` smoke runners (guarded by defines) |
| `worktrees/voice` | `Packages/com.imasu.steamworks.voice/`, `Ai Agent/modules/voice/` | — |
| `worktrees/lan` | `Packages/com.imasu.steamworks.lan/`, `Ai Agent/modules/lan/` | — |
| `worktrees/dedicated` | `Packages/com.imasu.steamworks.dedicated/`, `Ai Agent/modules/dedicated/` | — |
| `worktrees/character` | `Packages/com.imasu.character/` + `.multiplayer/`, `Ai Agent/modules/character/` | `Assets/LocalTest` controller demo scene |

## Merge order

Always **core first, addons second, game last**:

1. `com.imasu.steamworks` (and `com.imasu.character` if it changed) → main.
2. Addons that depend on them → main.
3. Game `Assets/` changes → main.

If a worktree branch touches a file another worktree also touched (rare, but scenes/prefabs and `Assets/LocalTest` can collide), resolve in `main` and rebase the younger worktree.

## Unity-specific worktree notes

- `Library/`, `Temp/`, `Logs/`, `UserSettings/` are gitignored — **each worktree re-imports the project** (slow first open, expected). Do not try to share one `Library/` across worktrees.
- Only **one Unity instance may have the project open** per worktree; two worktrees open simultaneously is fine because they are different folders.
- The Control Center's `EditorPrefs` (`ImasuSteamworks.ActiveConfigPath`) is machine-global, not per-worktree — picking a config in one worktree affects the other's window. Harmless, but know it.
- `.meta` files are committed. Rename/move package files via `git mv` so GUIDs (hence prefab/scene references) survive.

## Future Orca automation

When the first real worktrees are created, add an `orca.yaml` recipe for the module boundary contract above (which folders a worktree may touch, merge order, "core first" gate). Until then this file is the source of truth.