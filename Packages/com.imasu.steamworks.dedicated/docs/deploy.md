# Deploying the Imasu Dedicated Server

Working stub — full production hardening is a research task. This covers the happy
path for standing up a Linux headless server.

## Build

1. `Imasu → Steamworks → Dedicated → Build Linux Headless Server…` (or the button in
   the Control Center Modules tab).
2. Choose an output folder — for containers, use this package's `deploy/` folder so
   the `Dockerfile` at the package root picks it up.
3. The build copies the Active Config into `Assets/Resources/Config` (so it ships),
   gathers scenes from the active Build Profile (fallback: classic Build Settings),
   and builds with the **Server** subtarget (no graphics).

## Run (bare metal)

```sh
./DedicatedServer.x86_64 -batchmode -nographics -imasuDedicated \
    --port 7777 --maxPlayers 16 --map Game
```

CLI flags (each also has an env fallback for containers):

| Flag | Env | Meaning |
|------|-----|---------|
| `--port` / `-port` | `IMASU_PORT` | KCP listen port (0 = keep config `kcpPort`) |
| `--maxPlayers` / `-maxPlayers` | `IMASU_MAX_PLAYERS` | server cap (clamped 1–64) |
| `--map` / `-map` | `IMASU_MAP` | `matchScene` scene name to load |

## Run (Docker)

```sh
docker build -t imasu-dedicated .
docker run --rm -p 7777:7777/udp imasu-dedicated --port 7777
```

## Research backlog

- Server browser: advertise dedicated hosts over LAN discovery (`LanDiscovery`) so
  they appear in Browse Servers.
- Steam identity for the server process (game-server Steam API / anonymous login) if
  you want Steam-relay transport on dedicated hosts — today dedicated is KCP-only.
- Healthchecks, log rotation, auto-restart, resource limits in the image.
- Multi-map rotation (`ImasuNetworkManager.ServerChangeMap`).