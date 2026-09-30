# Technical notes — Imasu Steamworks

Architecture notes for the steamworks core + Control Center. Not a tutorial (that's `Docs/`); this is the "how it fits together" reference for agents working in this package.

## Runtime flow

- One global `ImasuSteamworksConfig` asset drives everything. `ImasuSteamworksMaster` (on the `ImasuSessionManager` prefab) holds an instance mirror and pushes values to components via `ApplyToComponents()`; only the first master survives (`DontDestroyOnDestroyOnLoad` singleton).
- Maps are **scene names inside `gameModes[] → maps[]`** — there are no per-map configs. Effective values resolve map → gamemode → global (`EffectiveMaxPlayers`, `EffectiveMinPlayers`, `EffectiveSecurity`, `EffectiveVoipMaxDistance`).
- `CatalogVersion` (static int, bumped in `ApplyToComponents`) lets UI refresh once per catalog change instead of per repaint.

## Unity 6 editor facts we verified

- Build Profiles: `UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()` + instance `scenes` property. The static `GetScenes`/`SetScenes` do **not** exist.
- Scene lookup: `AssetDatabase.FindAssets("t:SceneAsset")` is expensive — never call it per-frame. Use the Control Center's `SceneCache` (single pass, invalidated on `EditorApplication.projectChanged`).

## Control Center performance contract (D24)

- No `OnGUI` frame scans the project, scene, or package registry. All lookups go through caches.
- Spawn scans cache 1 s (invalidated on scene change, project change, or a spawn edit).
- `GetAllRegisteredPackages` cached, invalidated on `registeredPackages` event.
- The kill-switch toggle marks dirty without `SaveAssets` (dirty assets autosave).

## Addon pattern (D20)

Core defines interfaces (`IVoiceMember`, `ILanDiscovery`); addons implement them and are guarded by `defineConstraints`. Future: `IImasuModule` (Control Center tab registry) and `ICharacterNetworkAdapter`. Addons never reference core types directly by asmdef dependency on the addon side of the boundary.

## Addon Manager (2026-08-30)

- Editor surface `IImasuModuleEditor` (ModuleId/DisplayName/DrawSettings) in `Imasu.Steamworks.Editor.Addons`; discovered via `TypeCache.GetTypesDerivedFrom`. Runtime contract `IImasuModule` (ModuleId/DisplayName/Description/InstallUrl/IsEnabled(config)/Validate(config)) in `Imasu.Steamworks` — scaffolded for future play-mode validation.
- Install/Update/Remove use `UnityEditor.PackageManager.Client.Add(gitUrl)` / `Client.Remove(id)`. **Unity 6 UPM is poll-based** (`Request.IsCompleted`/`Status`/`Error` — there is NO `Completed` event). The Control Center polls in `OnGUI` and repaints via `EditorApplication.update` while a request is pending.
- Embedded detection: `PackageInfo.source == PackageSource.Embedded` → show "Installed (embedded)", no buttons (dev repo).
- **Catalog (2026-08-30):** `Packages/com.imasu.steamworks/Editor/Addons/catalog.json` (id/displayName/description/installPath/tag) is the source of truth; `LoadCatalog()` reads it (fallback hardcoded array), cached and invalidated on `projectChanged`/`registeredPackages`. Schema:
  ```json
  { "modules": [ { "id": "com.imasu.steamworks.lan", "displayName": "LAN (no Steam)",
                   "description": "...", "installPath": "Packages/com.imasu.steamworks.lan",
                   "tag": "com.imasu.steamworks.lan@0.1.0" } ] }
  ```
- Request types live in `UnityEditor.PackageManager.Requests` (not `UnityEditor.PackageManager`).

## Publish (2026-08-30, exercised)

- `.github/workflows/publish.yml` is triggered by a tag push `release/<package>@<version>`. It validates `package.json:version` == tag version, creates the OpenUPM-style **scoped tag** `<package>@<version>` (the tag consumers pin in git URLs), `npm pack`s a `.tgz`, and drafts a GitHub Release at that scoped tag. `permissions: contents: write` required.
- **Verified live:** pushed `release/com.imasu.steamworks@0.1.0`, `.voice`, `.lan`, `.dedicated` → 4 green runs, 4 scoped tags at `ed7d0b5`, 4 Releases each with `-0.1.0.tgz`. Consumer URL verified: `git clone --branch com.imasu.steamworks.lan@0.1.0` resolves and `Packages/com.imasu.steamworks.lan/package.json` is present.
- **Gotcha:** tag pushes made BEFORE GitHub has registered a newly-pushed workflow are silently dropped — re-push the tag to trigger. (All four were first pushed ~1 min after the workflow commit and nothing ran; delete + re-push fixed it.)
- **Consumer caveat:** `com.mirrornetworking.mirror` is not on any package registry (vendored only in this dev project), so a consumer resolving `com.imasu.steamworks` (declared dep `com.mirrornetworking.mirror: 96.11.0`) must supply Mirror first — vendor it into their `Packages/` the same way this repo does.

## Dedicated (2026-08-30)

`ImasuNetworkManager.StartDedicatedServer(port)` (server-only KCP) powers the `com.imasu.steamworks.dedicated` addon: `DedicatedHost` auto-boots on `-imasuDedicated`, loads config from Resources, Linux headless build helper. Details in `../dedicated/TECH.md`.