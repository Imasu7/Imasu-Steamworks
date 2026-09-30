# Project Health & Dependency Architecture

Repo-wide audit of packages, dependency graph, licensing, and the publish routine. Per-package
open issues live in `Ai Agent/modules/<name>/ISSUES.md`; this file is the whole-repo view plus
the constraints that shape how packages are installed and shipped.

## The one UPM constraint that shapes everything

Unity's docs are explicit: **Git dependencies cannot be declared in a `package.json`** — the
Package Manager supports git dependencies only in a *project's* `manifest.json`. Verified
against the `upm-git.html` reference for Unity 6000.0/6000.5.

Consequences:

- A published `com.imasu.*` package can never auto-pull its dependencies from git.
- The only UPM-legal mechanism is adding git URLs to the **consumer's** `manifest.json`, which
  is exactly what `UnityEditor.PackageManager.Client.Add(gitUrl)` does.
- Therefore dependency installation is a **bootstrap step** (Control Center "Install Steam
  dependencies"), not a `package.json` dependency. This is also why the Asset Store form ships
  as a one-click bootstrap rather than auto-resolving deps.

Two facts found while proving the consumer install:

- **Mirror's own repo has no UPM `package.json` at any tag** (it distributes via
  `.unitypackage` / Asset Store), so `https://github.com/MirrorNetworking/Mirror.git` cannot be
  git-installed into UPM. Fix: this repo hosts the vendored MIT v96.11.0 source as a git-path
  shim (`.../Imasu-Steamworks.git?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0`).
- **asmdef `versionDefines` do NOT fire for git-installed packages** (verified empirically:
  installing an addon never defined its symbol, so the addon assembly was skipped). Fix: the
  addons use the "installed == active" model — `defineConstraints`/`versionDefines` removed
  (D20).

## Dependency licensing (all MIT — verified 2026-08-31)

| Dependency | Source | License | Notes |
|---|---|---|---|
| Mirror (`com.mirrornetworking.mirror`) | MirrorNetworking/Mirror | MIT | vendored tag `v96.11.0`; `v96.11.1`/`v96.11.2` exist as newer patches |
| Facepunch.Steamworks | Facepunch/Facepunch.Steamworks | MIT | DLLs vendored in `com.imasu.facepunch` |
| FizzyFacepunch transport (`Mirror.FizzySteam`) | Chykary/FizzyFacepunch | MIT | vendored in `com.imasu.facepunch`; upstream latest release v4.4 |

All three are MIT, so they are redistributable with attribution and license preservation. The
design goes further: both are split out into their own **free** packages, so a sold
`com.imasu.steamworks` artifact ships no third-party code. Never re-vendor them into core.

## Dependency graph

```
com.imasu.steamworks.{voice,lan,dedicated}  ->  com.imasu.steamworks
com.imasu.steamworks                        ->  com.mirrornetworking.mirror   (vendored, free)
                                                   com.imasu.facepunch        (free)
```

The `voice -> lan` and `lan -> dedicated` edges do not exist and must not be added (D20).

## Release routine

1. Bump `version` in `Packages/<pkg>/package.json`, `catalog.json` module tags, and the
   `ModuleCatalogFallback` install URLs to the same value. Bump addons' `com.imasu.steamworks`
   dependency to the new core version. `com.imasu.facepunch` only bumps when its vendored
   binaries change.
2. Commit + push `main`.
3. Push `release/<pkg>@<ver>` for each changed package.
4. `publish.yml` validates `package.json` version == tag version, creates the scoped tag
   `<pkg>@<ver>`, packs a `.tgz`, and drafts a Release.
5. **Silent tag-drop gotcha (hit 0.1.0 and 0.1.1, not 0.1.2/0.1.3):** if no run appears within
   ~1 minute while the tag is on the remote, delete and re-push the `release/*` tag, or run the
   `workflow_dispatch` fallback with the same `pkg` + `ver`.
6. Verify: `git ls-remote --tags origin`, the Release has its `.tgz`, and a clone of the
   scoped tag resolves.

## Consumer proof (2026-08-31, still valid)

- A throwaway Unity 6 project added core + LAN + voice by git URL using the bootstrap URLs;
  Mirror and Facepunch resolved from the free shims; the result compiled with zero errors
  (core runtime + editor + LAN + Voice + FizzyFacepunch + all Mirror assemblies), and the
  Control Center assembly built.
- Headless CLI compile of the packages after the Facepunch split (0.1.2, 0.1.3) was clean.

## Current published versions (as of the 2026-09-30 split)

| Package | Version | Scoped tag |
|---|---|---|
| com.imasu.steamworks | 0.1.3 | `com.imasu.steamworks@0.1.3` |
| com.imasu.steamworks.voice | 0.1.3 | `com.imasu.steamworks.voice@0.1.3` |
| com.imasu.steamworks.lan | 0.1.3 | `com.imasu.steamworks.lan@0.1.3` |
| com.imasu.steamworks.dedicated | 0.1.3 | `com.imasu.steamworks.dedicated@0.1.3` |
| com.imasu.facepunch | 0.1.1 | `com.imasu.facepunch@0.1.1` |
| com.mirrornetworking.mirror | 96.11.0 | `com.mirrornetworking.mirror@96.11.0` |

## Parked questions

- Store submission packaging (`.unitypackage` + bootstrap UX) — outside this repo.
- Whether to expose a per-dependency list in the Package Manager UI or keep the single
  "Install Steam dependencies" button (currently the button).
- FizzyFacepunch vendored code predates upstream v4.4; attribution only, no intended functional
  difference.