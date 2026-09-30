# Changelog

Reverse-chronological, newest at the top. This file starts at the repo split on 2026-09-30;
the full history of the packages' development lives in the `Hidden Dev HDRP` game repo's
`Ai Agent/CHANGELOG.md`.

Agents: you **must** add an entry after every completed unit of work. See `RULES.md` section 2.

Format:

```
## YYYY-MM-DD — short title

- What happened (past tense)
- Files / packages touched
- Notes for the next agent
```

---

## 2026-09-30 — Install URLs repointed to the Imasu Steamworks repo

- Replaced every `Hidden-Dev-HDRP` reference in an install URL with `Imasu-Steamworks`:
  **26 replacements across 12 files**.
- Touched: `Packages/com.imasu.steamworks/Editor/ImasuPackageManager.cs`,
  `Packages/com.imasu.steamworks/Editor/Addons/catalog.json`, the `repository` metadata of the
  `com.imasu.*` `package.json` files, the addon runtime `InstallUrl` constants
  (`ImasuVoiceAddon.cs`, `ImasuLanAddon.cs`, `ImasuDedicatedAddon.cs`), the core and Facepunch
  package READMEs, and `Docs/steamworks/01-getting-started.html`.
- Notes for next: install URLs are the consumer contract — any change to them, the tag scheme,
  or `publish.yml` needs a `DECISIONS.md` entry (D28). `Packages/com.imasu.steamworks/README.md`
  still shows an old `@v0.1.0` fragment in one snippet; worth fixing on the next docs pass.

---

## 2026-09-30 — Scoped tags published on the new repo

- Created the consumer-facing scoped tags so git install URLs resolve:
  `com.imasu.steamworks@0.1.3`, `com.imasu.steamworks.voice@0.1.3`,
  `com.imasu.steamworks.lan@0.1.3`, `com.imasu.steamworks.dedicated@0.1.3`,
  `com.imasu.facepunch@0.1.1`, `com.mirrornetworking.mirror@96.11.0`.
- Notes for next: scoped tags are created by `publish.yml` from a `release/<pkg>@<ver>` trigger.
  These were pushed directly during the migration. If a tag-push run does not appear within
  ~1 minute, delete and re-push the `release/*` tag, or use `workflow_dispatch` (RULES.md 7).

---

## 2026-09-30 — Repo created: Imasu Steamworks split out of Hidden Dev HDRP

- Created `Imasu7/Imasu-Steamworks` as a standalone Unity UPM package monorepo, split out of
  the `Hidden Dev HDRP` game repo. No package versions changed in the move.
- Imported all six packages at their current versions: `com.imasu.steamworks` 0.1.3,
  `com.imasu.steamworks.voice` 0.1.3, `com.imasu.steamworks.lan` 0.1.3,
  `com.imasu.steamworks.dedicated` 0.1.3, `com.imasu.facepunch` 0.1.1,
  `com.mirrornetworking.mirror` 96.11.0 (vendored Mirror).
- Brought over `Docs/steamworks/`, `.github/workflows/publish.yml`, `README.md`,
  `tools/validate-dag.ps1`, the `.opencode/` agent config, and the `Ai Agent/` module docs
  (already package-scoped).
- Rewrote the repo-wide agent memory to describe the monorepo instead of the game: `AGENTS.md`
  created; `RULES.md`, `OVERVIEW.md`, `DECISIONS.md`, `CHANGELOG.md` rewritten; `STATUS.md`
  created; `GLOSSARY.md` trimmed to package terms; `WORKTREES.md` deleted (game-repo ORCA
  contract); `PROJECT_HEALTH.md` and `VERIFICATION.md` trimmed to package scope.
  New decisions D25–D28 record the monorepo, canonical-source, tag-scheme and install-URL
  choices.
- Notes for next: the game repo keeps embedded copies of the same packages for development, so
  changes must be pushed here before they reach consumers (D26). Development history before
  today is not duplicated here — read the game repo's changelog for it.