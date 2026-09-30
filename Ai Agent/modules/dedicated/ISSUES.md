# Issues — Imasu Steamworks Dedicated Servers

## Open

- [ ] **Smoke run pending** — `--smoke=dedicated` compiles into the smoke player but couldn't run in the headless shell (no graphics device). Run on a real machine to confirm `StartDedicatedServer` reaches `NetworkServer.active`.
- [ ] **Config in Resources** — the build helper copies the Active Config to `Assets/Resources/Config`; if the user changes the Active Config after building, the server runs the baked copy. Rebuild (or wire a CLI override) to pick up changes. CLI/env config overrides are planned for the steamworks core.
- [ ] **Scene list** — the Linux build uses the classic Build Settings scene list; keep it in sync via the Control Center's Sync Build Settings or the server won't contain new maps.