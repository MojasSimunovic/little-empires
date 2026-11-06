# Little Empires — Playable island sandbox

A zero-build Three.js exploration prototype for **Little Empires**. It is a companion to the Unity project, not a replacement for it.

## Play

Open the hosted demo in a current desktop browser with WebGL enabled. Click a building in the left panel, click any **empty island plot** to begin construction, and click **End Season** to complete buildings, produce and consume food, grow the settlement, and see its seasonal report. Click an existing Cottage to upgrade it to a Townhouse after construction finishes. Drag to rotate; scroll to zoom; reset/zoom controls are on the canvas.

The game autosaves in browser local storage. **Start a New Island** clears that saved state. Works on mobile, but desktop is recommended.

## Host for free without Node or Unity

1. Download the ZIP of these files (or the `demo/` folder of the repository).
2. In [Cloudflare Pages](https://dash.cloudflare.com/), go to **Workers & Pages → Create application → Drag and drop your files**.
3. Upload the ZIP or folder, choose a site name, and deploy.
4. Open the resulting `*.pages.dev` address.

Cloudflare Pages direct-upload documentation: https://developers.cloudflare.com/pages/get-started/direct-upload/

The game is static: no build step, bundler or npm dependencies to install. Three.js **0.180.0** is loaded from jsDelivr using a browser import map, so a network connection is needed. `file://` URLs may block JavaScript modules; use a hosted URL or a local HTTP server.

For local development on a personal computer with Python installed:

```bash
cd demo
python3 -m http.server 8000
# Then visit http://localhost:8000
```

## Files

- `index.html`: UI markup and Three.js import map.
- `style.css`: responsive game dashboard styling.
- `main.mjs`: terrain renderer, procedural building models, camera, mouse interactions and UI.
- `engine.mjs`: small, deterministic **JavaScript mirror** of the Unity-independent C# economy rules.
- `test-engine.mjs`: zero-dependency Node tests comparing against C# reference scenarios.

## Architecture and limitations

**Important:** The browser demo does **not** execute our C# assembly. The C# source under `src/LittleEmpires.Core/` remains authoritative. The JS rules are intentionally mirrored for a fast visual playtest. Keep the parity test scenarios updated when we change C# mechanics. To avoid two long-lived rules engines, we can later connect the Unity UI to the C# simulation and retire the JavaScript mirror.

The demo includes: island/camera, 5 buildable structures, upgrades, construction timers, resources, production, consumption, housing, immigration, happiness, season/year rollover, seasonal reports and local save.

The prototype does **not** implement workforce/jobs, terrain placement restrictions, roads, trade, diplomacy, AI rival islands, building animations or Council politics. Building costs and timings match our starter C# catalogue; initial resources are increased for easier hands-on experimentation.

No textures or 3D assets were downloaded. The stylized objects are assembled from Three.js primitives; all visible graphics are prototype-quality.