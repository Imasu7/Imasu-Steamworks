# Agent rules

These rules apply to every agent and every session in this repo. Read them before editing
anything.

If a user instruction and these rules conflict on *product* direction, the user wins. If they
conflict on *handoff hygiene* (changelog, status, version bumps), these rules win unless the
user explicitly overrides them.

## 1. Always load project memory first

Before planning or writing code, read:

1. This file
2. `Ai Agent/STATUS.md`
3. `Ai Agent/OVERVIEW.md` (package map)
4. `Ai Agent/CHANGELOG.md` (at least the latest entries)
5. `Ai Agent/DECISIONS.md`
6. `Ai Agent/modules/<name>/PLAN.md` for the package you are touching

Do not rediscover the architecture from scratch. Do not reopen decided questions unless the
user asks.

## 2. Always update CHANGELOG.md

This is the non-negotiable rule.

After **any** completed unit of work — docs, code, package install, bugfix, decision change —
append an entry to `Ai Agent/CHANGELOG.md` (newest first, `## YYYY-MM-DD — short title`).

A valid entry states the date, what changed in past tense, which packages/files were touched,
and anything a future agent must know (errors hit, Unity version notes, tag names).

Do **not** finish a turn that changed the repo without a changelog update. If work was
attempted and failed, log it as a blocked note so the next agent does not repeat it.

`STATUS.md` must also stay true: update it when a milestone moves next → done, the next task
changes, something becomes blocked, or a decision changes.

## 3. No game code in this repo

This repo is a reusable UPM package monorepo. `Packages/` is the product.

Never add game-specific code, assets, scenes, prefabs, roles, maps, gameplay, or anything
lives under `Assets/Hidden` or the game layer. Those belong to the `Hidden Dev HDRP` game
repo. Package code may only reference Unity, Mirror, Facepunch, and the interfaces defined
inside this repo.

If a change needs a game-specific concept, the concept belongs to the consumer. Model it as a
config field, an interface, or a prefab component — do not hard-code it.

## 4. Core never references an addon

`com.imasu.steamworks` compiles with **zero** references to `com.imasu.steamworks.voice`,
`.lan`, or `.dedicated`. Core owns the interfaces (`IVoiceMember`, `ILanDiscovery`,
`IImasuModule`); addons implement them and are active because they are installed
("installed == active"). Addons may depend on core; core must never depend on an addon.

Adding an `asmdef` reference from core into an addon assembly is a bug, not a design choice.

## 5. Vendored third-party code is never hand-edited

`Packages/com.mirrornetworking.mirror` is vendored MIT Mirror 96.11.0 (official repo ships
only a `.unitypackage`, so a UPM git dependency is impossible — see `DECISIONS.md` D4).

Update it by re-vendoring a new upstream tag, never by patching a file in place. Same rule
for the DLLs in `Packages/com.imasu.facepunch`.

`com.mirrornetworking.mirror` and `com.imasu.facepunch` are **not sold** — they are free MIT
dependencies the Control Center installs. Keep them out of any sold artifact.

## 6. Public API and behaviour changes need a version bump

Changing a package's public API or runtime behaviour requires:

1. Bumping `version` in that package's `package.json`.
2. A matching `Ai Agent/CHANGELOG.md` entry.
3. If an addon changed, bumping its `com.imasu.steamworks` dependency to the new core
   version.
4. Re-syncing `catalog.json` tags and any `InstallUrl` constants to the new version.

Docs-only or editor-only changes still need the changelog entry; they only need the version
bump if consumers can observe them.

## 7. Release procedure

1. Bump versions in `Packages/<pkg>/package.json`, `catalog.json`, and any `InstallUrl`
   constants to the same value.
2. Commit + push `main`.
3. Push the trigger tag for each changed package:
   `git tag release/<pkg>@<ver> && git push origin refs/tags/release/<pkg>@<ver>`
4. `.github/workflows/publish.yml` validates `package.json` version == tag version, creates
   the consumer-facing scoped tag `<pkg>@<ver>`, `npm pack`s a `.tgz`, and drafts a Release.
5. **Known gotcha:** GitHub sometimes silently drops a tag-push workflow run (hit 0.1.0 and
   0.1.1). If no run appears within ~1 minute while the tag is on the remote, delete and
   re-push the `release/*` tag. Fallback: `workflow_dispatch` with the same `pkg` + `ver`
   inputs runs the identical job.
6. Verify: `git ls-remote --tags origin` shows the scoped tag, the Release has the `.tgz`,
   and a consumer clone of the scoped tag resolves.

## 8. Document the repo, not the game

Any change to an **install URL**, the **tag scheme**, or the **publish workflow** needs a new
entry in `Ai Agent/DECISIONS.md` — those strings are the consumer contract and are spread
across `ImasuPackageManager.cs`, `catalog.json`, package `package.json` metadata, addon
runtime `InstallUrl` constants, package READMEs, and `Docs/steamworks/`.

Do not claim something worked unless you verified it (compile, play mode, or Steam test).

## 9. Editor performance contract (D24)

No `OnGUI` frame in an editor window may scan the project, the scene, or the package
registry. Use caches (`SceneCache`, spawn/package caches) and event-driven invalidation.
`AssetDatabase.FindAssets("t:SceneAsset")` is expensive and must never run per-frame. See
`Ai Agent/modules/steamworks/TECH.md`.

## 10. Unity 6 hygiene

- Target editor is Unity **6000.5.x**; the `com.imasu.*` packages declare `"unity": "6000.0"`.
- Replace obsolete APIs (`FindObjectOfType` → `FindFirstObjectByType` / `FindAnyObjectByType`,
  or better, cached references / singletons).
- Build Profiles: `UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()` plus the
  instance `scenes` property. The static `GetScenes` / `SetScenes` do not exist.
- Smoke/harness verification needs a machine with a display and (for Steam paths) the Steam
  client running. There are no unit tests in this repo — do not claim test coverage that does
  not exist.

## 11. Small, reversible steps

Prefer the next thin slice that compiles and can be described in `STATUS.md` over a large
rewrite.

## 12. End-of-session checklist

- [ ] `CHANGELOG.md` has today's work
- [ ] `STATUS.md` "Now / Next / Left" is accurate
- [ ] Version bumped in `package.json` for any public API / behaviour change
- [ ] New decisions are in `DECISIONS.md`
- [ ] No game code leaked into `Packages/`
- [ ] You did not claim something worked unless you verified it