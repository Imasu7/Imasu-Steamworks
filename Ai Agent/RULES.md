# Agent rules

These rules apply to every agent and every session on this project. Read them before editing anything.

If a user instruction and these rules conflict on *product* direction, the user wins. If they conflict on *handoff hygiene* (changelog, status, not dumping the old template into this project), these rules win unless the user explicitly overrides them.

## 1. Always load project memory first

Before planning or writing code, read:

1. This file
2. `Ai Agent/STATUS.md`
3. `Ai Agent/OVERVIEW.md` (module map)
4. `Ai Agent/CHANGELOG.md` (at least the latest entries)
5. `Ai Agent/DECISIONS.md`
6. The `PLAN.md` of the module you are working in (`Ai Agent/modules/<name>/`)

Do not rediscover the architecture from scratch. Do not reopen decided questions unless the user asks.

## 2. Always update CHANGELOG.md

This is the non-negotiable rule.

After **any** completed unit of work — docs, code, package install, bugfix, decision change — append an entry to `Ai Agent/CHANGELOG.md`.

A valid entry includes:

- Date (`YYYY-MM-DD`)
- What changed, in past tense
- Which files / packages / scenes were touched
- Anything a future agent must know (errors hit, Unity version notes, Steam App ID, etc.)

Do **not** finish a turn that changed the project without a changelog update.

If work was attempted and failed, log that too under a "Failed / blocked" note so the next agent does not repeat it.

## 3. Keep STATUS.md true

`STATUS.md` must match reality.

Update it when:

- A milestone moves from next → done
- The next task changes
- Something becomes blocked
- A decision in `DECISIONS.md` changes (also update that file)

`STATUS.md` is a snapshot, not a diary. The diary is `CHANGELOG.md`.

## 4. Do not merge the old Unity project into this one

The old Steamworks template (`UnityMultiplayer-MirrorAndSteamworks-Template`, Unity 2022.3 + URP) was **removed on 2026-08-24** — everything it contributed was ported (Mirror 96.11.0 vendored, Facepunch 2.4.1 + FizzyFacepunch in `Packages/com.hidden.steam-multiplayer/`). It is no longer needed and can be re-cloned from GitHub if ever required.

**Never** copy a URP template's `Assets/` tree, URP settings, TextMesh Pro dump, Mirror Examples, Edgegap hosting, or store assets into this repo.

Allowed: reading its lobby / network scripts and porting the *ideas* (and small cleaned-up C# files) into our UPM package.

## 5. Multiplayer lives in a package, not in game Assets

Reusable Steam multiplayer code belongs in a UPM package (`com.hidden.steam-multiplayer`), not scattered through `Assets/`.

`Assets/` is for the Hidden Source-style game (maps, characters, weapons, game modes).

Do not vendor a full copy of Mirror into `Assets/`. Add Mirror as a git/UPM dependency.

## 6. Scope discipline

Current package milestone (must work before Hidden gameplay):

- Steam create / join lobby
- Join screen
- Game setup screen (host)
- Waiting room screen
- Start match → spawn a networked dummy player
- Distance-based voice chat
- Local (non-Steam) host/client path for editor testing

Do **not** start Hidden vs IRIS gameplay, invisibility, weapons, or networking the HDRP sample third-person controller until the package milestone above works.

## 7. Unity 6 hygiene

- Target editor is **6000.5.9f1** (see `ProjectSettings/ProjectVersion.txt`).
- Replace obsolete APIs (`FindObjectOfType` → `FindFirstObjectByType` / `FindAnyObjectByType`, or better, cached references / singletons).
- Do not import URP assets into this HDRP project.
- Prefer Unity CLI compile checks after package or script changes when the CLI is available.

## 8. Steam testing rules

- Steam must be running for Steam lobby / voice tests.
- Default App ID during bring-up is **480 (Spacewar)** until a real App ID exists.
- Fizzy / Steam P2P cannot connect a client to itself. Local two-player tests use KCP/Telepathy, or two machines / two Steam accounts.

## 9. Docs stay beginner-friendly

When you add or change stack docs in `Ai Agent/stacks/`, write for a teammate who is new to code. Explain the *why* and the *everyday analogy*, then the technical how.

## 10. Small, reversible steps

Prefer the next thin slice that can compile and be described in STATUS.md over a large rewrite.

## 12. Module work updates the module's docs folder

Work on a module must also update that module's own docs (`Ai Agent/modules/<name>/`): its `PLAN.md`/`ROADMAP.md` status, `ISSUES.md`, or `TECH.md` as appropriate — not only the root files. Root `STATUS.md`/`CHANGELOG.md` still get the entry, but the module folder is where the module's long-term state lives. Do not edit another module's docs folder from a different module's work.

## 13. No per-repaint scans in editor windows (perf contract D24)

Editor windows that repaint on mouse move must never scan the project, the scene, or the package registry inside `OnGUI`. Use caches (`SceneCache`, spawn/package caches) and event-driven invalidation. See `modules/steamworks/TECH.md`.

## 11. End-of-session checklist

Before you stop:

- [ ] `CHANGELOG.md` has today's work
- [ ] `STATUS.md` "Now / Next / Left" is accurate
- [ ] New decisions are in `DECISIONS.md`
- [ ] You did not leave the old template merged in
- [ ] You did not claim something worked unless you verified it (compile, play mode, or Steam test)
