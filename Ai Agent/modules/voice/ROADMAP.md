# Roadmap — Imasu Steamworks Voice

Short addon roadmap. `VOICE` define gates the addon; installed via UPM.

- [x] Codec path (record → compress → decompress)
- [x] Distance attenuation + per-user volume
- [x] PTT + voice indicators + HUD
- [x] `IVoiceMember` core decoupling
- [x] Control Center tab + runtime module contract (via `IImasuModuleEditor` + `IImasuModule`)
- [ ] Cross-user relay verification
- [ ] Non-Steam voice fallback (research)

Tracked in the steamworks monorepo with scoped tags `com.imasu.steamworks.voice@…` (ROADMAP_2.0 convention, see `modules/steamworks/ROADMAP.md`).