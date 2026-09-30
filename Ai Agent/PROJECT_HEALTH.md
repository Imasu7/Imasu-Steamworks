# Project Health & Dependency Architecture

Living audit for the whole repo (packages, publishing, dependencies, modularity).
Created 2026-08-31 during the dependency-health pass. Per-module open issues live in
`Ai Agent/modules/*/ISSUES.md`; this file is the project-wide view + decisions.

## The one UPM constraint that shapes everything

Unity's docs are explicit: **Git dependencies cannot be declared in a `package.json`**
("The Package Manager doesn't support Git dependencies between packages. It supports
Git dependencies only for projects, so you can declare Git dependencies only in the
project's `manifest.json`"). Verified against `upm-git.html` (6000.0/6000.5).

Consequences:
- A published `com.imasu.*` package can NEVER auto-pull Mirror or Facepunch from git.
- The only UPM-legal mechanism is adding git URLs to the **consumer's** `manifest.json`,
  which is exactly what `UnityEditor.PackageManager.Client.Add(gitUrl)` does.
- Therefore dependency installation is a **bootstrap step**, not a `package.json` dep.

Second constraint discovered in the consumer proof (2026-08-31):
- **Mirror's own repo has no UPM `package.json`** (any tag) — it distributes via
  unitypackage / Asset Store, so `https://github.com/MirrorNetworking/Mirror.git` cannot
  be git-cloned into UPM. Fix: a free git-path shim in this repo
  (`?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0`, the
  vendored MIT v96.11.0 source).
- **asmdef `versionDefines` do NOT fire for git-installed packages** (verified
  empirically: installing `com.imasu.steamworks.lan@0.1.2` never defined `IMASU_LAN`, so
  the LAN assembly was skipped). Fix: addons use the standard **"installed = active"**
  model — `defineConstraints`/`versionDefines` removed; an addon compiles the moment it
  is installed.

## Dependency licensing (all MIT — verified 2026-08-31)

| Dependency | Source | License | Notes |
|---|---|---|---|
| Mirror (`com.mirrornetworking.mirror`) | MirrorNetworking/Mirror | MIT | git tag `v96.11.0` exists; `v96.11.1`/`v96.11.2` are newer patches |
| Facepunch.Steamworks | Facepunch/Facepunch.Steamworks | MIT | DLLs vendored in `com.imasu.facepunch` |
| FizzyFacepunch transport (`Mirror.FizzySteam`) | Chykary/FizzyFacepunch | MIT | vendored in `com.imasu.facepunch`; upstream latest release v4.4 |

All three are MIT → **legal to sell / redistribute** with attribution + license
preservation. Our design goes further: they are split OUT of the sold `com.imasu.steamworks`
package entirely and pulled free via the bootstrap, so the sold asset ships no third-party code.

Asset Store note: the store submission is a `.unitypackage`/UPM package that cannot auto-install
git deps. Buyers install it, then run the Control Center **"Install Steam dependencies"** once.
This is documented in the README + Control Center.

## Decisions (approved 2026-08-31)

1. **Split** Facepunch + FizzyFacepunch into a new embedded package `com.imasu.facepunch`
   (free, MIT, git-path installed). Core `.tgz` ships zero third-party code.
2. **Bootstrap**: Control Center "Install Steam dependencies" → `Client.Add(mirror#v96.11.0)` +
   `Client.Add(facepunch?path=…#com.imasu.facepunch@0.1.1)`; auto-offer before addon installs;
   core-first ordering.
3. **Mirror**: stays vendored in the game repo (`Packages/com.mirrornetworking.mirror`) — the
   dev sandbox stays pinned/offline; consumers get it via the bootstrap. Add Mirror's MIT LICENSE.
4. **`com.imasu.facepunch`** is versioned + scoped-tagged + released, folded into the existing
   publish routine. Low maintenance: only bump when the vendored binaries change.
5. **Delete** the Facepunch-containing `.tgz` assets from the old 0.1.0/0.1.1 releases after the split.
6. New issues file = this `PROJECT_HEALTH.md`.

## Audit findings (2026-08-31)

Severity-tagged. State column updated as fixes land.

| Sev | Finding | State |
|---|---|---|
| HIGH | Core `package.json` declares `com.mirrornetworking.mirror: "96.11.0"` (registry-style, not on any registry) → consumers can't resolve it | FIXED |
| HIGH | Core `.tgz` ships `Plugins/Facepunch` DLLs + `Runtime/FizzyFacepunch` transport (third-party binaries in the sold asset) | FIXED |
| HIGH | **Mirror's own repo has no UPM `package.json`** → not git-cloneable into UPM | FIXED (free shim in this repo) |
| HIGH | **asmdef `versionDefines` don't fire for git-installed packages** → addon assemblies never compiled for consumers | FIXED (defineConstraints removed; installed = active) |
| MED | Addons depend on `com.imasu.steamworks: "0.1.0"` (stale; `>=` semantics) | FIXED |
| MED | Core asmdef `versionDefines` expressions `"0.1.0"` (bare version = `>=`, so 0.1.1 still gates `VOICE`/`IMASU_LAN`) | FIXED (removed — mechanism broken for git) |
| LOW | No `license`/`repository`/`author`/`documentationUrl` fields + no LICENSE files in the 4 packages; vendored Mirror lacks Mirror's MIT LICENSE at package root | FIXED |
| LOW | Mojibake `�?"` in package descriptions (broken em-dash) | FIXED |
| LOW | Mirror `v96.11.2` exists (we stay on vendored `96.11.0`) | decision |
| LOW | Existing 0.1.0/0.1.1 release `.tgz` still contain Facepunch | FIXED (assets deleted) |

## Releasing (routine — codified 2026-08-31)

1. Bump `version` in the 4 imasu `package.json` files + `catalog.json` module `tag`s +
   the Control Center `ModuleCatalogFallback` tags (all to the same version). `com.imasu.facepunch`
   only bumps when its vendored binaries change (rare). Update addon `com.imasu.steamworks`
   dependency strings to the new core version.
2. Commit + push `main`.
3. Create + push `release/<pkg>@<ver>` for each changed package:
   `git tag release/com.imasu.steamworks@<ver> && git push origin refs/tags/release/<pkg>@<ver>`.
4. `publish.yml` validates package.json version == tag version, creates the scoped tag
   `<pkg>@<ver>`, packs a `.tgz`, drafts a Release.
5. **Silent tag-drop gotcha (hit 0.1.0, 0.1.1, NOT 0.1.2/0.1.3):** if a run doesn't appear
   within ~1 min (workflow active, tag on remote), delete + re-push the tag, or use the
   `workflow_dispatch` fallback (pkg + ver inputs). Check:
   `gh api repos/Imasu7/Hidden-Dev-HDRP/actions/workflows/346105174/runs`.
6. Verify: scoped tags (`git ls-remote --tags origin`), Releases with `.tgz`,
   consumer clone of the scoped tag.

## Verification

See `Ai Agent/VERIFICATION.md` (display-machine runbook) + the checklist below.

## Verification checklist (consumer proof)

- [x] Game repo: full CLI compile clean after the split (0.1.2, 0.1.3).
- [x] `--smoke=round` headless green in the rebuilt smoke player.
- [x] `packages-lock.json` regenerated (mirror + facepunch embedded).
- [x] **Throwaway consumer project (temp):** added core + LAN + voice via git URL
      (the exact bootstrap URLs) → Mirror + Facepunch auto-resolved from the free
      shims → **compiled with zero errors** (Imasu.Steamworks + Editor + LAN + Voice +
      FizzyFacepunch + all Mirror assemblies) → Control Center assembly built.
- [x] Published 0.1.2 (first split) then **0.1.3** (gating fix); `com.imasu.facepunch@0.1.1`
      + `com.mirrornetworking.mirror@96.11.0` scoped tags live. Old 0.1.0/0.1.1 release
      `.tgz` (which shipped Facepunch) deleted.

## Current published versions (2026-08-31)

| Package | Version | Scoped tag |
|---|---|---|
| com.imasu.steamworks (core) | 0.1.3 | com.imasu.steamworks@0.1.3 |
| com.imasu.steamworks.voice | 0.1.3 | com.imasu.steamworks.voice@0.1.3 |
| com.imasu.steamworks.lan | 0.1.3 | com.imasu.steamworks.lan@0.1.3 |
| com.imasu.steamworks.dedicated | 0.1.3 | com.imasu.steamworks.dedicated@0.1.3 |
| com.imasu.facepunch (deps) | 0.1.1 | com.imasu.facepunch@0.1.1 |
| com.mirrornetworking.mirror (shim) | 96.11.0 | com.mirrornetworking.mirror@96.11.0 |

## Open questions (parked)

- Asset Store submission packaging (`.unitypackage` + bootstrap UX) — outside this repo, tracked here.
- Whether to expose a `Dependencies` section in the Addon Manager UI vs. the single button (we use the button).
- FizzyFacepunch upstream version note: vendored code predates upstream v4.4; attribution only, no functional difference intended.