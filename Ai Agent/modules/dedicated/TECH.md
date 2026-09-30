# Technical notes — Imasu Steamworks Dedicated Servers

Built 2026-08-30.

- **Boot:** `DedicatedHost` has a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` bootstrap that checks the `-imasuDedicated` command-line arg and self-instantiates. `Start()` (coroutine) finds the `ImasuNetworkManager` (the `ImasuSessionManager` prefab in the boot scene), loads the config from `Resources.Load<ImasuSteamworksConfig>("Config/ImasuSteamworksConfig")`, assigns it to the `ImasuSteamworksMaster` + `ApplyToComponents()`, then calls `StartDedicatedServer(port)`.
- **Transport:** server-only KCP (no Steam session, no UI). `ImasuNetworkManager.StartDedicatedServer(int port)` (new core method) forces the KCP transport, optionally sets `kcp2k.KcpTransport.Port`, and calls Mirror `StartServer()`.
- **Build:** `DedicatedBuildHelper` copies the Active Config into `Assets/Resources/Config/ImasuSteamworksConfig.asset` (Resources always ships in builds) then `BuildPipeline.BuildPlayer` targeting `StandaloneLinux64`, scenes from `EditorBuildSettings.scenes`.
- **Addon boundary:** depends on core only (D19/D20). Registers a Control Center tab via `IImasuModuleEditor` (editor surface) — no define needed since the addon is additive (no core edits beyond `StartDedicatedServer`).