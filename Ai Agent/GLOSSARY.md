# Glossary

Short definitions for people new to this kind of repo. Package-level detail lives in
`Ai Agent/modules/<name>/TECH.md`.

**Addon** — A separate UPM package that adds one capability to `com.imasu.steamworks` (voice, LAN, dedicated server). Addons implement interfaces defined in core and depend on core; core never depends on an addon. "Installed == active": there is no switch that turns an addon off.

**AudioSource** — A Unity component that plays sound. In 3D mode the volume falls off with distance from the listener.

**Catalog** — The list of gamemodes and maps a project offers, held in the config asset under `gameModes[] -> maps[]`. `catalog.json` is a different thing: the editor-side list of packages the Package Manager window can install.

**Client** — A player connected to someone else's session.

**Dedicated server** — A machine that runs the game with no local player. Provided by `com.imasu.steamworks.dedicated`.

**Embedded package** — A package folder placed under a project's `Packages/` folder so Unity compiles it from source with no download. That is how all six packages live in this repo.

**Facepunch.Steamworks** — The C# library used to talk to Steam (init, lobbies, friends, voice, overlay).

**FizzyFacepunch** — The Mirror transport that carries game data over Steam Networking Sockets / Relay instead of a raw IP address.

**Guard define** — A compile-time symbol (for example `IMASU_LAN`) that limits which code an assembly compiles. Addons are gated on being installed, not on a guard define; the define mechanism exists only where a project genuinely needs it.

**Host / listen server** — One player's machine runs the server and plays in it; others connect to it.

**Host migration** — When the host leaves, a remaining player is promoted to host and the session survives instead of collapsing.

**KCP** — Mirror's fast IP-based UDP transport. Used for local editor testing and LAN play, because Steam P2P cannot connect a client to itself.

**Lobby** — The Steam-side waiting room: members, settings, and invites, created before the match scene loads.

**Master** — `ImasuSteamworksMaster`, the component on the Session Manager prefab that mirrors the config asset and pushes values to every other component via `ApplyToComponents()`. Only the first one survives as a `DontDestroyOnLoad` singleton.

**Mirror** — The networking library: it tracks who is host, spawns networked objects, and keeps their state in sync.

**NAT / relay** — Home routers block unsolicited inbound connections. Steam Relay is Steam's detour, so two players can connect without opening ports.

**NetworkIdentity** — The Mirror component that marks an object as networked. Without it other players cannot see it as a synced object.

**OpenUPM** — A public Unity package registry. These packages are not on it; the tag naming follows its `<package>@<version>` convention, and the tags are consumed directly from git.

**Package / UPM** — Unity Package Manager. A folder with a `package.json` that any project can add by path or by git URL.

**PCM** — Raw audio samples (the waveform). Steam Voice compresses PCM for transmission; the receiver decompresses it back to PCM to play.

**Push-to-talk** — Voice only transmits while a key is held.

**Scoped tag** — A git tag named `<package>@<version>` (for example `com.imasu.steamworks@0.1.3`). It is what consumers pin in an install URL; it is created by the publish workflow, not by hand.

**Session Manager** — The prefab a game places in each playable scene. It carries the master, the network manager, and the session UI contract.

**Steam App ID** — The number Steam uses to identify an app. `480` (Spacewar) is the development ID used for bring-up; a shipped game gets its own from Steamworks.

**Steam Voice** — Steam's microphone capture and compression. It carries no distance information — the package does distance attenuation in Unity.

**Transport** — The layer that moves bytes: KCP over IP, or FizzyFacepunch over Steam.

**uGUI** — Unity's built-in UI system. The package builds its session screens in code rather than shipping hand-authored scenes.

**Vendored** — Third-party source copied into this repo under `Packages/` and re-updated by hand from an upstream tag. `com.mirrornetworking.mirror` is vendored; never edit it in place.