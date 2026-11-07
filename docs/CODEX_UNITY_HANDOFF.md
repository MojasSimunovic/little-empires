# Little Empires — handoff from ChatGPT project conversation to local Codex + Unity MCP
_Updated 2026-11-07. Use repository code and the live Unity Editor as the source of truth if they disagree with this snapshot._

## Goal

Build an original, charming, strategic island-builder called **Little Empires**. The user is a front-end developer who designed the gameplay in ChatGPT, then playtested the Three.js strategic island through **Year 5** and confirmed construction, terrain rules, roads, production, taxes, workforce and Marketplace felt good. They have now moved to a Windows PC, opened a **Unity 6 URP** project, generated the first actual C#-powered island, and installed **MCP for Unity v10.3.0** with Python/uv dependencies. They want Codex to take over hands-on Unity editing with the local MCP connection.

The user loves the atmosphere of **Tiny Glade**: warm, lush, storybook-like medieval villages with organic gardens, charming roofs, light and subtle animation. The reference is about **mood**, not copying that game's procedural building mechanics. For our strategy game, keep grid-based validation under the visuals, but reduce obvious checkerboard terrain and card-board-looking geometry. The free **Quaternius Medieval Village MegaKit** is the preferred starting point for modular architectural prefabs; use its license terms and avoid pushing bundled assets into a public repo.

## Repository layout

- `unity/LittleEmpires/`: current Unity 6 project (URP). Might contain local changes not pushed; inspect Git and Unity.
- `unity/starter/Assets/LittleEmpires/`: starter scripts, assembly definitions, Unity Editor menu to create scene.
- `tools/Install-UnityStarter.ps1`: copies starter and shared C# sources into existing Unity project.
- `src/LittleEmpires.Core/`: authoritative, deterministic simulation targeting .NET 8 with a separate netstandard2.1/C#9 compatibility project.
- `src/LittleEmpires.Core/WorldGameSession.cs`: primary Unity-facing API (`Snapshot()`, `PreviewBuilding`, `PlaceBuilding`, `PlaceDirtRoad`, `UpgradeStoneRoad`, `UpgradeBuilding`, `ChangeTaxes`, `ChangeWorkPriority`, `Trade`, `EndSeason`).
- `src/LittleEmpires.Simulator/`: deterministic console scenarios.
- `tests/LittleEmpires.SmokeTests/`: smoke and strategic economy tests.
- `demo/`: older Three.js playable demo. Not the final game.

## Existing gameplay (implemented)

- Seeded, valid islands with water, grass, forests, fertile land and mountain tiles, with building placement restrictions.
- Build housing (Cottage, Townhouse upgrade), Lumber Camp, Fishery, Quarry, Farm, Lumber Mill, Food Processing Plant, Marketplace.
- Free dirt roads, stone-road upgrades, four-neighbor connected road network; building/road tile exclusivity.
- Seasonal production; +50% connected Lumber Camp output from operational Lumber Mill; +25% connected Farm/Fishery food from Food Processing Plant (no stacking).
- Population, housing, resident food consumption, happiness, seasonal immigration.
- Strategic economy workforce demands/priority, operating coin upkeep, taxes Low/Normal/High/VeryHigh affecting income and happiness.
- Player-driven Marketplace buy/sell of wood/stone/food with spread, trade quotas, connectivity and stock checks.
- At the end of the season, update resources, workers, happiness, population, building progress, and season/year.

The Three.js version worked, but is NOT directly powering Unity. Unity uses **real C# `WorldGameSession`**.

## What was seen running in Unity (screenshots, November 7)

The user successfully executed `Little Empires > Create Starter Island Scene` and pressed Play. The Game view showed the generated 3D island and OnGUI controls. At first the Console emitted `InvalidOperationException` because the legacy `UnityEngine.Input` API was disabled. They were instructed to set **Edit > Project Settings > Player > Active Input Handling = Both**. In a subsequent screenshot the Game view rendered closely zoomed-in cottages and environment, with Console showing zero current errors. The prototype GUI takes up too much of the small Game window and covers the map.

MCP for Unity was installed through `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`; package window displayed **v10.3.0**. Initial setup reported Python missing and uv/git present. User later confirmed **the MCP package is set up**. No read-only Codex MCP connection test has yet happened, so verify it instead of assuming.

## User's desired workflow

- Open Codex **locally** in the ChatGPT desktop app (Codex view).
- Open repository root `little-empires` as local project, not the `unity/LittleEmpires` subdirectory alone, so Codex can see C# core and docs.
- Set up **unityMCP** server in Codex Settings > MCP servers, or via MCP for Unity's Client Configuration UI. The MCP plugin typically exposes Streamable HTTP on `http://localhost:8080/mcp` when configured for HTTP, but confirm the URL/port in the Unity plugin rather than assuming. Use only local loopback, do not expose MCP publicly.
- Test connection by fetching Unity scene/hierarchy, active scene and Console errors before making edits.
- Have Codex work on actual scene/material/prefab assets and scripts; review and commit changes to Git in small chunks.

**ChatGPT chat history does not automatically follow into Codex**. This file plus `AGENTS.md`, the source code, issue tracker and live Unity Editor are the durable handoff.

## Suggested first Codex assignment

1. Confirm the Unity MCP bridge and Codex are connected, then inspect `unity/LittleEmpires/`: project version, current scenes, assets, Input settings, Console errors, game hierarchy, source-sync status, Git changes and installed packages.
2. Restore or regenerate the runnable `StarterIsland` scene without discarding the existing user's scene or uncommitted changes. Stop Play mode before editing scenes.
3. Fix prototype UX: readable sidebars, no clipping/overlap at 1080p or narrower, responsive canvas and better camera zoom/framing. Preserve existing economic controls and C# integration.
4. Make one small **art-direction vignette**: single cozy cottage, organic ground, dirt road, foliage and warm late-afternoon sunlight. Prefer MegaKit prefab variations if legitimately available; otherwise create a rough in-engine style study while waiting for assets.
5. Verify at least: Cottage placed on valid land; invalid placement blocked; End Season updates population/resources; roads connect; Play mode Console stays clean; ask the user to approve the atmosphere before large-scale replacement of all assets.
6. Report exactly which changes were tested vs only edited; keep commits reviewable.

## Unity starter implementation and known rough edges

The starter scene builds `WorldGameSession.NewStrategicIsland(42)`, renders `WorldSnapshot.Terrain`, `Roads`, `Buildings` through `IslandView`, and uses `GameBootstrap` OnGUI UI. The scene and camera are intentionally provisional. The current UI is cramped/overlays the island; no final prefab library, save system, or polished terrain exists. The user does not mind restarting island playtests.

The Unity project's `Assets/` may currently be newer than the GitHub checkout: **inspect local filesystem first**. The initial GitHub upload included a URP project structure but did not include every generated runtime script/scene when last checked, so do not infer Editor state solely from the remote commit.

## Priority links and docs

- GitHub: https://github.com/MojasSimunovic/little-empires (private; authorized account must have access)
- Unity epic: https://github.com/MojasSimunovic/little-empires/issues/35
- Subtasks: #36 C# compatibility; #37 island; #38 placement/roads; #39 economy UI; #40 polish/save/build.
- Docs: `docs/UNITY_HANDOFF.md`, `docs/STRATEGIC_ECONOMY.md`, `docs/WORLD_RULES.md`, `docs/ROADS.md`, `docs/BUILDINGS.md`, `unity/GETTING_STARTED_WINDOWS.md`.

## Design north star

`"I want my village to feel lovely even when I am not clicking anything."`

Keep the economics meaningful, but the presentation cozy, inviting, legible and full of small environmental life.
