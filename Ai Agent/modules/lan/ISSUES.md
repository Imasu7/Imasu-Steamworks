# Issues — Imasu Steamworks LAN

- [ ] **No voice on LAN** — voice uses Steam capture APIs; a non-Steam fallback is unplanned. PTT hides with "Voice requires Steam".
- [ ] LAN browse on multi-NIC machines needs testing (discovery may see itself / wrong subnet).

## Resolved

- "Leave Lobby bounces back" — fixed 2026-08-24 (session teardown + per-frame view enforcer conflict).