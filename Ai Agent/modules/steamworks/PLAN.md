# Plan

This is the agreed plan. Change it only with the user, then log the change in `CHANGELOG.md` and `DECISIONS.md`.

## Goal

Ship a **reusable Unity 6 multiplayer package** that gives Hidden Dev (and later projects) a working Steam game session:

- Create / join a Steam lobby
- **Join screen**
- **Game setup screen** (host configures the match)
- **Waiting room screen** (player list, ready, host start)
- Listen-server gameplay over Steam Relay (NAT-friendly)
- **Distance-based voice chat** (Steam carries the voice; Unity handles how loud it is by distance)
- A local non-Steam host/client path so we can test in the editor without Steam P2P

Only after that package is real do we build Hidden Source gameplay on top of it.

## Why a package, not a dump, not a .unitypackage

The old template is a whole URP game on Unity 2022.3. Copying it would fight HDRP and drag in Mirror examples, store assets, and old settings.

A `.unitypackage` (the classic "Unity asset") copies files into `Assets/` and is hard to version. The modern equivalent is a **UPM package**: a folder with a `package.json` that any Unity 6 project can add by path or git URL.

We develop that package **inside this project** (Hidden Dev is still a stock HDRP template, so it is already a clean sandbox). We do not create a third Unity project just to export an asset.

Proposed layout when we start code:

```
Unity Projects/
  Hidden Dev HDRP/                      <- this game
    Packages/
      com.mirrornetworking.mirror/      <- Mirror 96.11.0 (vendored, Slice 1 done)
      com.hidden.steam-multiplayer/     <- reusable package (embedded, Slice 2 started)
        Runtime/                        <- lobby, steam, voice, network manager, transport
        Plugins/                        <- Facepunch DLLs + steam_api native libs
        Samples~/                       <- join / setup / waiting-room UI (later)
    Assets/                             <- Hidden maps, characters, weapons (later)
```

Decision D12: the package is embedded under `Packages/` in this repo, not a sibling folder. New networking code builds directly inside it — no staging area needed.

## Stack (summary)

Details and beginner explanations: `Ai Agent/stacks/`.

| Layer | Choice | Role |
|-------|--------|------|
| Engine | Unity 6000.5.9f1 | Game editor / runtime |
| Look | HDRP | High-end PC lighting, Hidden Source atmosphere |
| Netcode | Mirror (current, via git URL) | Who is host, spawn players, sync objects |
| Steam API | Facepunch.Steamworks first | Lobbies, friends, overlay, voice capture |
| Steam transport | FizzyFacepunch | Mirror talks to Steam Networking Sockets |
| Local test transport | KCP (or Telepathy) | Editor host/client without Steam |
| Voice distance | Unity AudioSource 3D rolloff | Steam has no proximity volume; we do it in-engine |
| UI | uGUI (Unity UI) | Join / setup / waiting room |
| Reuse | UPM package `com.hidden.steam-multiplayer` | Drop into new projects |

Fallback if Facepunch/FizzyFacepunch will not compile or run on 6.5: **Steamworks.NET + FizzySteamworks**, and port the small lobby scripts.

We are **not** using Unity Netcode for GameObjects for this. The template already has a Mirror + Steam lobby flow; Hidden Source is a small listen-server game; mixing NGO and Mirror would be worse.

## Player flow

```
Join screen
  ├─ Create game  → Game setup screen (host: map, player count, voice range, …)
  │                    → Waiting room (host + joiners, ready, invite friends)
  │                         → Host starts → load game scene → spawn players + enable voice
  └─ Join game    → pick lobby / accept invite
                       → Waiting room
                            → Match starts with the others
```

Local editor flow skips Steam and uses KCP on localhost (one editor as host, a build as client, or two editor instances if Multiplayer Play Mode is available later).

## Screens (package responsibility)

### Join screen

- Create game
- Join by Steam friends / open lobbies / invite
- Optional: "Local LAN / editor test" button that uses KCP
- Show Steam name and connection state (Steam running or not)

### Game setup screen (host only)

Minimum fields for the package milestone:

- Lobby name
- Max players
- Map / scene to load (dropdown of registered scenes)
- Voice max distance (metres)
- Public vs friends-only lobby

These values are stored as Steam lobby data (so joiners can see them) and re-applied when the match scene loads.

### Waiting room screen

- Player list (Steam avatar + name)
- Host / not host
- Ready toggle (nice-to-have for v1; host Start is required)
- Invite friends
- Leave lobby
- Host: Start match (loads the chosen scene, Mirror `ServerChangeScene`)

The old template already starts Mirror host on lobby create and client on lobby enter. We keep that pattern unless testing shows we should delay `StartHost` until the waiting room. Decision: start the Mirror session when the Steam lobby is created/joined so voice and player list can use the same connections; the "game scene" is a second scene change.

## Distance-based voice

Steam Voice gives us compressed microphone audio. It does **not** know where players stand, so it cannot fade volume with distance.

Plan:

1. Local player holds push-to-talk (or a toggle) → Steam Voice records and compresses.
2. Compressed bytes go to other players over Steam Networking (unreliable, low latency). Not over Mirror's reliable channel.
3. Each remote player object has an `AudioSource` at the head.
4. We decompress Steam voice to PCM and feed that `AudioSource`.
5. The `AudioSource` is **3D**: `spatialBlend = 1`, logarithmic rolloff, configurable min/max distance from game setup.
6. If the speaker is farther than max distance, we simply do not play (or volume is already zero).
7. Later (not v1): occlusion, team-only radio channel, dead-player mute.

v1 is proximity-only. That matches Hidden Source: you hear people who are actually near you.

## Implementation slices

Do these in order. Each slice should leave the project compiling.

### Slice 1 — Mirror compiles ✅ (2026-08-22)

- ~~Add latest Mirror via UPM git URL to `Packages/manifest.json`~~ → **vendored** `Packages/com.mirrornetworking.mirror/` (official repo has no `package.json`; see D4)
- Do not import Examples — done (Excluded Examples/Hosting/Tests)
- Unity CLI compile check — passed, exit 0

### Slice 2 — Steam transport compiles ✅ (2026-08-22)

- Facepunch plugins + native `steam_api` → `com.hidden.steam-multiplayer/Plugins/Facepunch/`
- FizzyFacepunch → `com.hidden.steam-multiplayer/Runtime/FizzyFacepunch/` (asmdef refs Mirror)
- `steam_appid.txt` = 480 for editor — added at repo root
- Fix Unity 6 obsolete API in any copied script — none needed (no `FindObjectOfType`, etc.)
- Unity CLI compile check — passed, exit 0

### Slice 3 — Local session ✅ (2026-08-22)

- Reusable `HiddenNetworkManager` (extends NetworkManager, StartLocalHost/Client, StartMatch) in `com.hidden.steam-multiplayer/Runtime/Network/`
- Dummy player prefab with `NetworkIdentity` + `NetworkTransformReliable` (generated by `Assets/LocalTest/Editor/LocalTestSceneBuilder.cs`)
- Host and client on localhost KCP
- Scene change host → Game scene, both spawn a dummy
- Verified via standalone build, two instances (`--smoke=host` / `--smoke=client`), `SMOKE_HOST_OK` + `SMOKE_CLIENT_OK`

### Slice 4 — Steam lobby session (host path ✅ 2026-08-22)

- Port / rewrite SteamManager, SteamLobbyManager → `com.hidden.steam-multiplayer/Runtime/Steam/`
- Create lobby → StartHost — **verified live** (SteamSockets server started)
- Join lobby → StartClient using lobby owner Steam ID — **implemented, join unverified** (needs second Steam account/machine)
- Two Steam users can enter the waiting room and start — pending join-path test

### Slice 5 — The three screens ✅ (2026-08-22)

- Join, setup, waiting room as **runtime-built uGUI** (`HiddenSessionUI`) in the package — no URP materials, TMP text
- `LobbySettings` (name / max players / scene / voice range / public) stored as Steam lobby data; host writes on create, joiners read on enter
- Wire buttons to SteamLobbyManager / HiddenNetworkManager (no inspector-only magic)
- `SessionScreens.unity` sample scene + `--smoke=ui` standalone verification (dummy spawned over local KCP)

### Slice 6 — Voice ✅ (2026-08-22, codec path verified)

- `VoicePlayer` (NetworkBehaviour on player prefab): capture via `SteamUser.VoiceRecord`, send compressed bytes on Mirror **unreliable** channel (`[Command]`/`[ClientRpc]`, `Channels.Unreliable`)
- Receive: `SteamUser.DecompressVoice` → PCM → streamed AudioClip → 3D `AudioSource` (spatialBlend=1, logarithmic rolloff, maxDistance = `LobbySettings.voiceRange`)
- Distance rolloff config verified in build (`SMOKE_VOICE_CONFIG`); record→compress→decompress verified live (`SMOKE_VOICE_OK`); audible two-user relay still needs a second Steam user
- PTT toggle added to the waiting room UI

### Slice 7 — Package fold-up

- Move reusable code into `com.hidden.steam-multiplayer`
- Game project depends on it via `file:` path
- Sample scene stays as a package sample
- README in the package for "add to a new Unity 6 project"

## Re-scope (2026-08-23): production flow fixes ✅

The original "create game skips to game" bug was the scene builders setting
`onlineScene = "Game"`. Reworked into a production layout and verified:

- **R1** — separate scenes: `MainMenu` (offline) → `WaitingRoom` (online) → `Game` (match). `HiddenNetworkManager.matchScene`; `StartMatch()` → `ServerChangeScene(matchScene)`.
- **R2** — human UI: main menu (create / join by code / local host / local client / quit), waiting room with lobby code + Copy, pause menu (Resume / Back to Lobby / Quit to Menu / Quit), back buttons, blank-field fix, `HiddenSession.prefab`.
- **R3** — `AiCube` server-driven sync demo verified (client saw it move).
- **R4** — HDRP materials fixed (real .mat assets + `ResetMaterialKeywords`).
- KCP forced for local tests (transport-selection bug).

## Out of scope until the package works

- Hidden vs IRIS roles
- Invisibility, aura, IRIS night vision / flashlight
- Weapons, damage, round loop
- Replacing the HDRP sample character with a networked FPS controller
- Dedicated servers
- Console / mobile
- A third blank Unity project used only to bake a `.unitypackage`

## Verification bar

Do not mark a slice done in STATUS.md unless:

- Scripts compile (Unity CLI or editor)
- For slice 3+: a host and a client both see the dummy player
- For slice 4+: Steam is running and a second user (or second machine) joined
- For slice 6+: walking away from a talking player lowers volume

## Reference code we mined (template removed 2026-08-24)

The old template (`UnityMultiplayer-MirrorAndSteamworks-Template`) was used as reference only and is now **removed** — its useful pieces were ported and live in this project:

- `SteamManager` / `SteamLobbyManager` → `Packages/com.hidden.steam-multiplayer/Runtime/Steam/`
- `CustomNetworkManager` / local-test launcher ideas → `Runtime/Network/HiddenNetworkManager.cs` + `Assets/LocalTest/`
- `Assets/Facepunch/` + `Assets/FizzyFacepunch-master/` → `Plugins/Facepunch/` + `Runtime/FizzyFacepunch/` (port, namespaced `Mirror.FizzySteam`)
