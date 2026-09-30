# Agent entry point

This repo is a **Unity UPM package monorepo** (the Imasu Steamworks multiplayer stack).
It keeps its living brief in **`Ai Agent/`**.

If you are a new agent (or this terminal was lost), read these in order before doing any
work:

1. [`Ai Agent/RULES.md`](Ai%20Agent/RULES.md) — required behaviour: changelog updates, version
   bumps, no game code in packages, release procedure
2. [`Ai Agent/STATUS.md`](Ai%20Agent/STATUS.md) — what is true right now, what is next
3. [`Ai Agent/OVERVIEW.md`](Ai%20Agent/OVERVIEW.md) — package map, layer diagram, runtime flow
4. [`Ai Agent/CHANGELOG.md`](Ai%20Agent/CHANGELOG.md) — what already happened
5. [`Ai Agent/DECISIONS.md`](Ai%20Agent/DECISIONS.md) — repo-wide locked decisions
6. The `PLAN.md` of the package you are working in:
   [`Ai Agent/modules/steamworks/PLAN.md`](Ai%20Agent/modules/steamworks/PLAN.md),
   [`Ai Agent/modules/voice/PLAN.md`](Ai%20Agent/modules/voice/PLAN.md),
   [`Ai Agent/modules/lan/PLAN.md`](Ai%20Agent/modules/lan/PLAN.md),
   [`Ai Agent/modules/dedicated/PLAN.md`](Ai%20Agent/modules/dedicated/PLAN.md)

Do not skip `RULES.md`. Every completed piece of work must update `CHANGELOG.md`; work on a
package must also update that package's own docs folder (`Ai Agent/modules/<name>/`). Any
change to a package's public API or behaviour needs a `version` bump in its `package.json`
plus a matching changelog entry.

## What lives where

| Path | Contents |
|---|---|
| `Packages/` | The six UPM packages. **Nothing else.** No game-specific code. |
| `Docs/steamworks/` | Consumer-facing offline HTML manual (open `index.html`). |
| `.github/workflows/publish.yml` | Tag-driven publish: `release/<pkg>@<ver>` in, scoped tag + `.tgz` + draft Release out. |
| `tools/validate-dag.ps1` | Fails loudly if a blocked model is bound to an agent node. |
| `Ai Agent/` | Agent memory: rules, status, overview, decisions, changelog, per-package docs. |
| `README.md` | Package table and install snippet. |

## The one thing to know before you touch anything

`Packages/` is a **reusable product**, not an app. The game that consumes it (`Hidden Dev
HDRP`, a separate repo) keeps embedded copies under its own `Packages/` for development.
Anything game-specific — scenes, roles, maps, gameplay — belongs in that game repo, never
here. See `Ai Agent/RULES.md` section 3.

Per-package reference docs: `Docs/steamworks/`, each package's `README.md`, and
`Ai Agent/modules/<name>/TECH.md` for how it fits together.