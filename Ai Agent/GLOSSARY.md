# Glossary

Short definitions for people new to this kind of project. Stack docs in `stacks/` go deeper.

**Agent** — An AI coding assistant working in this repo. These markdown files are how one agent hands work to the next.

**Assets folder** — Unity's `Assets/` directory. This is where *this game's* scenes, models, and scripts live. Reusable multiplayer code should *not* sprawl here; it belongs in a package.

**AudioSource** — A Unity component that plays sound. If it is "3D", the sound gets quieter (and can pan left/right) based on how far the listener (usually the camera / player) is.

**Client** — A player who connected to someone else's game. They are not the host.

**Dedicated server** — A computer that runs the game with no player on it. We are *not* doing this for v1. The host is a player.

**Deprecation** — A warning that an old function still works but Unity wants you to use a newer one. Example: `FindObjectOfType` is deprecated in Unity 6; use `FindFirstObjectByType`.

**Facepunch.Steamworks** — A C# library that talks to Steam (lobbies, friends, voice, overlay) with a relatively friendly API.

**FizzyFacepunch** — A small adapter so Mirror can send game data through Steam's networking instead of a raw IP address.

**HDRP** — High Definition Render Pipeline. Unity's high-end graphics path (lighting, fog, reflections). This project uses it. The old template uses URP instead, which is why we must not copy its materials/scenes.

**Host / listen-server** — One player's machine runs the server *and* they play on it. Friends connect to them. Like hosting a LAN game in older PC titles.

**Hidden Source** — A Half-Life multiplayer mod: one "Hidden" predator (almost invisible) hunts a team of IRIS agents. Our game's design reference.

**KCP** — A fast, IP-based network transport that comes with Mirror. We use it for local testing because Steam P2P cannot connect you to yourself.

**Lobby** — A Steam waiting area before the match. People join, see who is there, then the host starts the real game scene.

**Mirror** — A Unity multiplayer library. You mark objects as networked; Mirror keeps them in sync between host and clients.

**NAT / relay** — Home routers block random incoming connections. Steam Relay is Steam's detour so two players can still talk without opening ports.

**NetworkIdentity** — A Mirror component that says "this object exists on the network." Without it, other players cannot see that object as a synced thing.

**Package / UPM** — Unity Package Manager. A folder with a `package.json` that Unity can add to a project. Our multiplayer kit will be one of these, so a new project can add it in one step.

**P2P (peer to peer)** — Players connect to each other (with Steam helping) rather than all talking to a rented server.

**PCM** — Raw audio samples (the waveform). Steam Voice compresses PCM to send it, then we decompress back to PCM to play it.

**Push-to-talk** — Voice only sends while you hold a key. Typical for this genre; stops the lobby sounding like an open mic.

**Render pipeline** — The graphics engine inside Unity (HDRP, URP, or Built-in). Mixing two pipelines in one project is a mess.

**Steam App ID** — The number Steam uses for an app. `480` is Spacewar, Valve's sandbox ID for testing. A real game gets its own ID from Steamworks.

**Steam Voice** — Steam's microphone capture and compression. It does *not* handle distance. We do distance in Unity.

**Transport** — The "how bits travel" layer under Mirror: KCP over IP, or Fizzy over Steam.

**uGUI** — Unity's built-in UI system (canvases, buttons, text). We use this for join / setup / waiting room.

**Unity CLI** — Command-line Unity, so an agent can compile the project without clicking around the editor.

**.unitypackage** — An older "zip of Assets" format. Easy to import, hard to update. We prefer UPM instead.

**URP** — Universal Render Pipeline. What the old template uses. Not what this game uses.
