# Imasu Steamworks

A **reusable Unity 6 multiplayer package** that gives any game a working Steam game session: lobbies, networking, voice, and dedicated servers.

## Packages

| Package | Description | Version |
|---------|-------------|---------|
| `com.imasu.steamworks` | Core — lobbies, session manager, spawns, Control Center | 0.1.3 |
| `com.imasu.steamworks.voice` | Steam VOIP (distance-based voice chat) | 0.1.3 |
| `com.imasu.steamworks.lan` | LAN discovery (no Steam required) | 0.1.3 |
| `com.imasu.steamworks.dedicated` | Headless dedicated server | 0.1.3 |
| `com.imasu.facepunch` | Facepunch.Steamworks DLLs + FizzyFacepunch transport | 0.1.1 |
| `com.mirrornetworking.mirror` | Vendored Mirror 96.11.0 | 96.11.0 |

## Quick Start

### Install (Unity 6.5+)

1. Open **Imasu → Steamworks Control Center**
2. Click **"Install Steam dependencies"** (adds Mirror + Facepunch)
3. Install addons via **Imasu → Package Manager** or git URL:
   ```
   https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.steamworks.lan#com.imasu.steamworks.lan@0.1.3
   ```

### Manual Install

Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.imasu.steamworks": "https://github.com/Imasu7/Imasu-Steamworks.git?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.1.3"
  }
}
```

## Requirements

- Unity 6000.5.9f1+
- HDRP 17.5+ (or URP — pipeline-agnostic)
- Steam App ID (use 480 for development)

## Documentation

- **Control Center**: `Imasu → Steamworks Control Center` in Unity
- **Offline docs**: `Docs/steamworks/` (open `index.html`)
- **Package READMEs**: Each package has its own `README.md`

## License

MIT (see individual package LICENSE files)
