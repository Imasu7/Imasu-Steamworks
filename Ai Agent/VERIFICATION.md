# Verification Runbook (display machine)

Everything here needs a real machine with a display (and §4 needs a second Steam
account/machine). The agent can't init graphics headlessly; the only headless-verifiable
runner is `--smoke=round` (already green via CLI). Follow in order; tick the box + note
`PASS/FAIL` + any log line in the Results table.

**Prereqs:** Steam client **running + logged in** (appid 480), the game project open in
Unity 6000.5.9f1, and `Builds/LocalTestSmoke/HiddenDevHDRP.exe` (rebuilt 2026-08-31).
Player.log: `%USERPROFILE%\AppData\LocalLow\Imasu\Hidden Dev HDRP\Player.log`.

---

## §1 — Control Center hand-test (Unity editor)

Open `Imasu → Steamworks Control Center`. Hover/nav every tab — expect **no stutter** (D24).
Tabs: **Home | Project & Session | Gamemodes & Maps | Spawns & Teams | Advanced**.

- **Header:** Active Config shows the project preset; **Package Manager** button opens the
  package window. On reopen after a config change, expect the config to **auto-apply**
  (no "misconfigured" / unselect-reselect).
- **Gamemodes & Maps:** each gamemode is a foldout that expands in place — click the arrow,
  it opens (the old "dead arrow" is gone). Add via **+ Add Gamemode**; reorder with ▲▼;
  Remove works. Inside a mode: **Maps** shows a labeled **"Menu name | Scene name"** header;
  selecting a map row shows its detail with a real **Scene** picker (pick the scene — no
  mystery HDRP). **Overrides** rows show named fields (Max Players, VOIP Enabled, …) with a
  checkbox — unticked = inherit global.
- **Project & Session / Spawns & Teams / Advanced:** fields commit on Enter/click-away;
  Advanced no longer shows a second gamemode editor (that tab owns them).
- **Validate Maps:** click → toast; bottom bar shows **"Scene whitelist: N/N map scene(s) valid"**.
- Bottom bar: **Apply Active Config**, **Add Session Manager**, **Open Docs** respond.

## §1b — Imasu Package Manager (new window)

Open `Imasu → Package Manager`. Core + addons listed with status; **Steam dependencies**
(Mirror + Facepunch) show **"installed"** in this repo (embedded) and the install button is
disabled. Filter + clear work. Character shows "Planned — not yet published".

## §2 — Player smokes (build: `Builds/LocalTestSmoke/HiddenDevHDRP.exe`)

> **Two-instance note:** the session prefab auto-inits Steam on boot; the SECOND player
> instance fails to start if Steam isn't running. Keep Steam logged in for the pair smokes.
> `--smoke=round` covers the same localhost network path headlessly and needs no Steam.

```powershell
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=ui            # SMOKE_UI_HOST_OK
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=lan-flow      # SMOKE_LAN_STEP4 + STEP7
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=dedicated --port 7777   # SMOKE_DEDICATED
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=round         # SMOKE_ROUND_OK (headless-verified, re-run for completeness)
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=combat       # SMOKE_COMBAT_OK (headless-verified, re-run for completeness)
# pair (needs Steam): two terminals —
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=host          # waits, SMOKE_HOST_OK on 2nd player
& "Builds\LocalTestSmoke\HiddenDevHDRP.exe" --smoke=client        # SMOKE_CLIENT_OK localPlayer=DummyPlayer
```

Pass = the `SMOKE_*_OK`/`STEP` lines appear in the Player.log.

## §3 — Dedicated Linux artifact

```powershell
# Unity editor:
Imasu/Steamworks/Dedicated/Build Linux Headless Server…   # pick an output folder
# on the Linux box (or WSL):
./DedicatedServer.x86_64 -batchmode -nographics -imasuDedicated --port 7777 --maxPlayers 16 --map Game
# join from the game: Join Game → type <server-ip>:7777
# container (from Packages/com.imasu.steamworks.dedicated/):
docker build -t imasu-dedicated .
docker run --rm -p 7777:7777/udp imasu-dedicated --port 7777
```

Pass = server logs `[Dedicated] Overrides — port=7777 maxPlayers=16 map=Game` then KCP
listening; client connects and reaches the map. (Full runbook: `Packages/com.imasu.steamworks.dedicated/docs/deploy.md`.)

## §4 — Cross-user Steam (second account/machine)

- Host on account A (Steam running, appid 480) → create lobby → share the 7-char code.
- Machine B: **Join Game → code** → waiting room shows both → host Start Match → both reach the map.
- **Round-1 gameplay:** roles assign (one Hidden); IRIS aim-hold tags the Hidden → IRIS
  round win screen + score; Hidden survives the timer → Hidden win.
- **Round-2 combat (hand-pass):** as IRIS, aim at the Hidden — the bottom lock bar fills
  (green = LOCKED) — press **LMB** → Hidden eliminated → "IRIS eliminated the Hidden".
  As the Hidden, get within ~3 m of an IRIS and press **LMB** → that IRIS eliminated →
  alive counts drop; eliminate everyone → "Hidden eliminated every IRIS".
- **Voice:** PTT, speak; the other user hears by distance (walk away → fades); watch `[Voice] TX/RX` logs.
- **Host migration:** host A clicks Leave Lobby with B still in → B should auto-promote
  ("Became the new host after migration") and the lobby survive; B's waiting room shows
  host controls. Optionally have a third user verify reconnect.
- **Chat:** type in the waiting-room chat panel → both sides see it; Enter sends.

## §5 — Consumer install (GUI click-through — resolution already proven headlessly)

In a throwaway Unity 6 project with the Control Center open (install core via git URL
first: `...?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.1.3`):
1. **Imasu → Package Manager → "Install Steam dependencies"** → both show "installed (v…)".
2. **Install com.imasu.steamworks.lan@0.1.3** → poll → row flips to "Installed (v0.1.3)".
3. No compile errors in Console; the LAN row's inline settings pane appears.

---

## Results

| # | Check | Result | Notes / log lines |
|---|-------|--------|-------------------|
| 1 | Control Center hand-test | | |
| 2a | --smoke=ui | | |
| 2b | --smoke=lan-flow | | |
| 2c | --smoke=dedicated | | |
| 2d | --smoke=round | | |
| 2e | --smoke=combat | | |
| 2f | host/client pair | | needs Steam |
| 3 | Linux dedicated + Docker | | |
| 4a | Join by code + map | | |
| 4b | Roles + IRIS fire win | | |
| 4c | Hidden lunge win | | |
| 4d | Two-way voice / fade | | |
| 4e | Host migration | | |
| 4f | Lobby chat | | |
| 5 | Consumer bootstrap GUI | | |