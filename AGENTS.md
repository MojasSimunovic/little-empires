# AGENTS.md — Little Empires agent instructions

You are helping build **Little Empires**, a cozy, seasonal, isometric island-building strategy game. This is a real Unity 6 URP project paired with a pure C# simulation, not a greenfield prototype.

## Start here

1. Read `docs/CODEX_UNITY_HANDOFF.md` for the design brief and current implementation status.
2. Inspect `unity/LittleEmpires/`, especially `Packages/manifest.json` and `Assets/`, and check current Git status before changing anything.
3. If running on the user's Windows PC, connect to the already-installed **MCP for Unity** server and inspect its Editor connection, current scene, Console and project state. If the server is unavailable, say so and explain what the user needs to enable; never claim to have modified Unity.
4. Prioritize small, visible, playable improvements and test them in Unity where possible.

## Architecture: non-negotiables

- `src/LittleEmpires.Core/` is the **single authoritative source of economic and world simulation rules**. Unity only renders `WorldGameSession.Snapshot()` and sends commands through `WorldGameSession`.
- The core must remain independent of `UnityEngine`; it builds for net8.0 and netstandard2.1/C# 9. Keep console, smoke tests and CI working.
- Synchronize core into Unity with `tools/Install-UnityStarter.ps1` (do not create a second fork of the economy logic).
- `demo/` is an older playable Three.js prototype and reference for UI and mechanic feel; do not rewrite it unless specifically asked.
- Preserve `Assets/*.meta`, project settings and package manifest under Git. Exclude Unity's `Library`, `Temp`, `Obj`, generated builds and secrets. Do not commit third-party asset pack files unless licensing explicitly allows repository distribution.
- Inspect local, possibly uncommitted, Unity work before overwriting or re-running installers; avoid destructive scene or prefab edits without a backup/commit.

## Product/art direction

- Tiny Glade is the main **atmosphere reference**, not a request to clone its systems: warm cream walls, terracotta roofs, varied medieval architecture, lush foliage, flowers, cozy sunlight, gentle life and seasonal changes.
- Player sees a small readable island in an elevated orbiting camera. Keep a **logical tile/grid economy** but soften/hide the visible grid with natural ground, terrain, roads and decorations.
- The user likes **Quaternius Medieval Village MegaKit** (free standard pack) as a modular foundation for cottages, farms, workshops and mills; adapt palettes and prefabs, and use only properly licensed assets.
- Aim for tasteful, performant stylized visuals; plain cubes are scaffolding, not the final target.
- The user wants to playtest iteratively, with working controls before visual complexity.

## First local Codex + Unity mission

1. Verify MCP Unity connection with a **read-only scene/hierarchy/Console inspection**; confirm active project path and source files.
2. If not already present, ensure the seeded Unity starter scene exists and runs (see `unity/GETTING_STARTED_WINDOWS.md`). Confirm changes to core compile and Unity Play mode works.
3. Fix camera framing and the oversized, overlapping prototype OnGUI HUD at 1080p and smaller resolutions. Prefer a clean UI Toolkit/uGUI strategy if appropriate, but keep economic commands routed to C#.
4. Then build **one beautiful cottage and small surrounding vignette** (road, greenery, garden, warm lighting) as an art-direction proof of concept. Use MegaKit only after importing it in Unity and verifying its license.
5. Keep progress measurable: show the updated Game view, test click-to-build and End Season, inspect Console, and report what changed and what is unverified.

## Key references

- `docs/CODEX_UNITY_HANDOFF.md`: this conversation's compact handoff
- `unity/GETTING_STARTED_WINDOWS.md`: Windows starter and source-sync commands
- `docs/UNITY_HANDOFF.md`: core API mapping
- `docs/STRATEGIC_ECONOMY.md`: industry, workers, taxes and trade
- `docs/WORLD_RULES.md` and `docs/ROADS.md`: terrain/building/road rules
- `src/LittleEmpires.Core/WorldGameSession.cs`: Unity-facing game command interface
- `tests/LittleEmpires.SmokeTests/`: rules tests
- `unity/starter/Assets/LittleEmpires/`: starter MonoBehaviours and scene menu
- GitHub epic #35 and tasks #36–#40.
