# Verification Runbook

What has to be checked by hand for these packages, and what cannot be checked here at all.

**Constraints, stated up front:**

- There are **no unit tests in this repo**. Nothing below is automated.
- Every editor or player check needs a **machine with a display**. A headless shell cannot init
  graphics, so anything that touches uGUI or audio has to be run by a human.
- Steam paths need the **Steam client running and logged in** (dev App ID 480). Steam P2P cannot
  connect a client to itself, so two-player Steam checks need a second account or machine.
- The `--smoke=*` player harness lives in the consuming game repo (`Hidden Dev HDRP`), not here.
  Those runs verify this repo's packages, but the harness itself is not part of this repo.

Follow in order. Tick the box and record `PASS`/`FAIL` plus any log line in the Results table.

---

## §1 — Control Center hand-test (Unity editor, consumer project)

Open `Imasu -> Steamworks Control Center`. Hover and navigate every tab; expect **no stutter**
(D24). Tabs: **Home | Project & Session | Gamemodes & Maps | Spawns & Teams | Advanced**.

- **Header:** Active Config shows the project preset; the **Package Manager** button opens the
  package window. Reopening after a config change should **auto-apply** — no
  "misconfigured" / unselect-reselect dance.
- **Gamemodes & Maps:** each gamemode is a foldout that expands in place. Add via **+ Add
  Gamemode**; reorder with the arrows; Remove works. Inside a mode, **Maps** shows a labeled
  "Menu name | Scene name" header and the detail has a real **Scene** picker. **Override** rows
  show a named field with a checkbox; unticked = inherit global.
- **Project & Session / Spawns & Teams / Advanced:** fields commit on Enter or click-away.
- **Validate Maps:** click it; the bottom bar reports "Scene whitelist: N/N map scene(s) valid".
- Bottom bar: **Apply Active Config**, **Add Session Manager**, **Open Docs** all respond.
- Build settings: add a map, confirm it lands in both classic Build Settings and the active
  Unity 6 **Build Profile** scene list.

## §2 — Imasu Package Manager window

Open `Imasu -> Package Manager`. Core plus the addons are listed with status; the free Steam
dependencies (Mirror, Facepunch) show **"installed"** when embedded and their install button is
disabled. Filter and clear work. Install failures must read **"Install failed: <reason>"**,
not a stale cryptic error.

## §3 — Consumer install (GUI click-through)

The git-URL path was already proven to compile headlessly in a throwaway project
(`Ai Agent/PROJECT_HEALTH.md`). What remains is the GUI:

1. In a clean Unity 6 project, install core by git URL
   (`.../Imasu-Steamworks.git?path=Packages/com.imasu.steamworks#com.imasu.steamworks@0.1.3`).
2. `Imasu -> Package Manager -> "Install Steam dependencies"` -> both rows show
   "installed (v...)".
3. Install `com.imasu.steamworks.lan` -> the row flips to "Installed".
4. No compile errors in the Console; the LAN row's inline settings pane appears.

## §4 — Session flow, two real users

Needs Steam on both machines (App ID 480).

- Host on account A creates a lobby; share the 7-character code.
- Account B: **Join Game -> code** -> both appear in the waiting room -> host starts the match ->
  both reach the map scene.
- **Host migration:** A leaves the lobby with B still in it -> B is promoted (log line
  "Became the new host after migration"), the lobby survives, and B sees the host controls.
- **Chat:** type in the waiting-room chat panel -> both sides see it; Enter sends.
- **Voice:** push-to-talk, speak; the other user hears it fade with distance; check the
  `[Voice] TX/RX` logs. Confirm the distance mode follows the config (fixed vs range).

## §5 — Dedicated server artifact

```powershell
# Unity editor:
Imasu/Steamworks/Dedicated/Build Linux Headless Server...   # pick an output folder
# on the Linux box (or WSL):
./DedicatedServer.x86_64 -batchmode -nographics -imasuDedicated --port 7777 --maxPlayers 16 --map Game
# container (from Packages/com.imasu.steamworks.dedicated/):
docker build -t imasu-dedicated .
docker run --rm -p 7777:7777/udp imasu-dedicated --port 7777
```

Pass = the server logs `[Dedicated] Overrides - port=7777 maxPlayers=16 map=Game` then KCP
listening, and a client reaches the map. Full runbook:
`Packages/com.imasu.steamworks.dedicated/docs/deploy.md`. Remember the config is baked into
`Resources` at build time — rebuild after a config change.

---

## Results

| # | Check | Result | Notes / log lines |
|---|-------|--------|-------------------|
| 1 | Control Center hand-test | | |
| 2 | Package Manager window | | |
| 3 | Consumer bootstrap GUI | | |
| 4a | Join by code -> match scene | | needs 2 Steam accounts |
| 4b | Host migration | | needs 2 Steam accounts |
| 4c | Lobby chat | | needs 2 Steam accounts |
| 4d | Two-way voice / distance fade | | needs 2 Steam accounts |
| 5 | Linux dedicated + Docker | | |