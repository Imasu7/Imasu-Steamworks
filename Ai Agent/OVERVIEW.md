# Overview — Hidden Dev HDRP (project-level)

**Last updated:** 2026-08-30

This is the project-level overview. It describes the **game**, the **module map**, and how the pieces relate. Per-module planning lives in `Ai Agent/modules/<name>/`.

## What the project is

A Unity **6000.5.9f1 HDRP** game meant to play like **Hidden Source** (the Half-Life hide-and-seek mod): one nearly-invisible predator vs a team of hunters, Steam multiplayer, proximity voice.

Two engineering layers:

1. **Imasu modules** — reusable Unity packages (`com.imasu.*`), game-agnostic, developed inside this project and published later by git URL.
2. **The Hidden game** — the HDRP scenes, maps, characters and future gameplay in `Assets/`, built on top of the Imasu modules.

## Module map

| Module | Package | Status | Docs |
|--------|---------|--------|------|
| **Imasu Steamworks** (core) | `com.imasu.steamworks` | Working (config, lobbies, session manager, spawns, Control Center) | `modules/steamworks/`, `Docs/steamworks/` |
| Imasu Steamworks Voice | `com.imasu.steamworks.voice` | Working addon (Steam VOIP) | `modules/voice/` |
| Imasu Steamworks LAN | `com.imasu.steamworks.lan` | Working addon (LAN discovery, no Steam) | `modules/lan/` |
| Imasu Steamworks Dedicated Servers | `com.imasu.steamworks.dedicated` | **Planned** (no code) | `modules/dedicated/` |
| Imasu Character Controller | `com.imasu.character` | **Planning** (no code) — roadmap in `modules/character/PLAN.md` | `modules/character/`, `Docs/character/` |
| Imasu Character Controller Multiplayer | `com.imasu.character.multiplayer` | **Planned** (addon of the controller) | `modules/character/` |

Dependencies (always through public interfaces, never by direct type reference across modules):

```
com.imasu.character.multiplayer ──→ com.imasu.character  +  com.imasu.steamworks
com.imasu.steamworks.voice ──────→ com.imasu.steamworks
com.imasu.steamworks.lan ────────→ com.imasu.steamworks
com.imasu.steamworks.dedicated ──→ com.imasu.steamworks
com.imasu.steamworks ────────────→ com.mirrornetworking.mirror  (vendored)
```

`com.imasu.character` depends on nothing Imasu (standalone single-player by design).

## How work is organized

- `Ai Agent/` = project memory (rules, status, changelog) + per-module planning under `modules/`.
- `Docs/` = consumer-facing offline HTML, organized by module.
- Code lives in `Packages/com.imasu.*/` (packages) and `Assets/` (game only).
- Each module is worktree-ready: one package folder + one docs folder, developed independently (see `WORKTREES.md`).

## Where the game stands

The reusable multiplayer foundation is working (Steam lobbies, session manager, spawns, voice, LAN, Control Center). Hidden Source gameplay (roles, invisibility, weapons, maps, the character controller) has **not started** and waits behind the package milestone (see `modules/steamworks/PLAN.md`).

## Files in this folder

| File | Purpose |
|------|---------|
| `RULES.md` | Agent rules (changelog rule, scope discipline, Unity 6 hygiene). |
| `STATUS.md` | Current snapshot: what is done, next, blocked. |
| `CHANGELOG.md` | Append-only history of all work. |
| `DECISIONS.md` | Project-wide decisions + index into per-module decision files. |
| `OVERVIEW.md` | This file — project/module overview. |
| `WORKTREES.md` | ORCA worktree contract + module boundary rules. |
| `GLOSSARY.md` | Beginner definitions. |
| `stacks/` | Human-readable software-stack explainers. |
| `modules/` | Per-module planning: PLAN, ROADMAP, DECISIONS, ISSUES, TECH. |