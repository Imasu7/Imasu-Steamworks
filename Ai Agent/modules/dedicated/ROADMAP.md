# Roadmap — Imasu Steamworks Dedicated Servers

- [x] `IImasuModule` registry + Addon Manager in core (editor surface `IImasuModuleEditor`)
- [x] Headless `DedicatedHost` entry point (`-imasuDedicated`)
- [x] Linux headless build helper (Build Profile scenes + Server subtarget) + config → Resources
- [x] Server logs + smoke runner (`--smoke=dedicated`)
- [x] CLI/env overrides (`--port`/`--maxPlayers`/`--map` + `IMASU_*` env)
- [x] Dockerfile stub + `docs/deploy.md`
- [ ] Smoke run on a real machine (needs a display)
- [ ] Docker hardening + server-browser advertising (research backlog)

Status: **built (2026-08-30, slices 1–5 of the overnight build)**, runtime smoke pending. Never bundled with LAN.