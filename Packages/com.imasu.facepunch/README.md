# Imasu Facepunch (Steam transport dependencies)

Free, MIT-repackaged dependencies for **Imasu Steamworks**, installed automatically
by the Control Center's **"Install Steam dependencies"** button. This package is not
sold and contains no original code from Imasu:

- **Facepunch.Steamworks** — the Steamworks API wrapper DLLs (`Plugins/Facepunch/`)
- **FizzyFacepunch** — the Mirror transport `Mirror.FizzySteam` (`Runtime/FizzyFacepunch/`)

Both are MIT licensed; see `LICENSE.md` for attribution. The native `steam_api*`
binaries belong to Valve and ship under the Steamworks SDK license.

## Why this package exists

`com.imasu.steamworks` (the sold product) ships **zero** third-party code. These
dependencies are pulled at install time from this free package via Unity's Package
Manager. Unity does not support git dependencies between packages, so the Control
Center adds this package (and Mirror) to your project's `manifest.json` for you.

## Manual install (if you're not using the Control Center)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.imasu.facepunch": "https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/com.imasu.facepunch#com.imasu.facepunch@0.1.1",
    "com.mirrornetworking.mirror": "https://github.com/Imasu7/Hidden-Dev-HDRP.git?path=Packages/com.mirrornetworking.mirror#com.mirrornetworking.mirror@96.11.0"
  }
}
```

Then add `com.imasu.steamworks` the same way (`?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.1.3`).

> Mirror note: Mirror's own GitHub repo ships no UPM `package.json` (it's distributed as a
> unitypackage / on the Asset Store), so it can't be git-cloned into UPM. This repo hosts a
> free git-path shim (`Packages/com.mirrornetworking.mirror`, the vendored MIT v96.11.0
> source) so the bootstrap can add it. If you prefer, install Mirror from the Asset Store
> instead and skip that line.

## Versioning

`com.imasu.facepunch` is versioned + tagged (`com.imasu.facepunch@x.y.z`) and only
bumped when the vendored binaries change (rare).