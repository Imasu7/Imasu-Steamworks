# Imasu — Offline Docs

This is a **static HTML** documentation site, organized by module. It opens by double-clicking
`Docs/index.html` — **no server, no npm, no build step** at read time.

## Layout

```
Docs/
  index.html                  all-modules landing page (module map + dependencies)
  styles.css                  shared theme
  img/placeholders/           drop your screenshots here, then replace the
                              placeholder divs in each page
  steamworks/                 Imasu Steamworks manual (module docs)
    index.html                steamworks overview
    01-getting-started.html   add package, required pieces, ImasuSession
    02-master-window.html     Control Center: every setting (reference)
    03-voice.html             Fixed vs Range, per-user VOIP volume
    04-lobby-flow.html        host / join / minPlayers / leaving
    05-ui-templates.html      the 6 copy-paste template scripts
    06-testing.html           smoke builds + CLI flags
    07-troubleshooting.html   common issues + log paths
  character/                  Imasu Character Controller (planning)
    index.html                roadmap overview
```

Voice/LAN/Dedicated addons are covered inside the steamworks manual (03-voice etc.) and in the
planning docs at `Ai Agent/modules/`. When an addon ships its own manual it gets its own folder
here too.

## Adding a screenshot

1. Drop the image into `Docs/img/placeholders/` (or `Docs/img/`).
2. Find the matching placeholder in a page:

   ```html
   <div class="screenshot-placeholder">
     <!-- SCREENSHOT: Control Center window -->
     ...
   </div>
   ```

3. Replace it with an `<img>`:

   ```html
   <img src="../img/placeholders/control-center.png" alt="Control Center window" style="max-width:100%">
   ```

   (paths are relative to the page — subfolder pages use `../img/...`).

## Editing

All pages share `styles.css` and a sidebar nav. Subfolder pages link the stylesheet as
`../styles.css`. Change the sidebar in every page if you add a new section. Keep content
beginner-friendly — the "why" and the everyday analogy first, then the technical how.

## Porting to Docusaurus (future)

The user asked for Docusaurus. If you later convert:

1. Scaffold: `npx create-docusaurus@latest site classic`
2. Copy the prose from these HTML pages into `site/docs/*.md` (Markdown), preserving the module folders.
3. Configure `docusaurus.config.js` with `baseUrl: "./"` and `trailingSlash: false`
   so the built site works from `file://`.
4. `npm run build` and commit `site/build/` — double-click `build/index.html`.

The static HTML version is the reliable fallback until that conversion is done.