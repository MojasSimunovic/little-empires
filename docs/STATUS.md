# Little Empires — current status (stabilization review)

Updated 2026-11-08. This file describes the **committed code**, not a guarantee that every feature has passed Unity Play Mode.

## Working/implemented in C# and Unity source
- Unity 6 URP `LargeIsland` scene, 64×64 gameplay map and authored `LargeIslandGeography` for generally level city land, one mountain region, beach/coastline.
- Unity runtime terrain/river mesh, cottage prefab registry, real `Meadow Cottage.prefab`, orbital/pannable camera, OnGUI controls.
- Core economy, construction, housing upgrade rules, seasons, workers, food, taxes and marketplace; .NET smoke tests and simulator.
- Road path planner and click/drag release with green/red previews; dirt→stone upgrades with batch costs and C# atomic validation. River bridge models and bridge data representation are in the project. Four-way road and house adjacency.
- Three authored bridge crossings; bridge type is selected by the road surface at a permitted river crossing.
- CI builds C# for net8 and Unity-compatible netstandard2.1, runs tests and simulator. **Branch `fix/stabilization-claude-audit` adds a guard against core/Unity source drift.**

## Active stabilization issues
- [Review PR #46](https://github.com/MojasSimunovic/little-empires/pull/46) (draft, not merged). It proposes fixes for G-1/G-2 river alignment and mouth, G-3 crossing discoverability, G-4 safe End Season key, G-5 selection outline, P-1 snapshot cache, R-1 script sync CI, R-2 ignore/remove generated IDE files, and this status documentation.
- **T-1 / T-2:** Fishery shoreline foundations and Quarry mountain plots are too steep; need physically flat pads/ledges, numeric slope tests and Unity screenshots. No claim of fix yet.
- **P-2:** `IslandView.Rebuild` still recreates buildings on world refresh (roads are more incremental). Plan a dictionary of building instances keyed by ID/index and update changed ones only.
- **T-3/T-4/T-6:** coarse square biome masks, procedural flat-color textures and no imported Quaternius Nature MegaKit vegetation in playable LargeIsland. Terrain art pass still pending.
- **B-1:** URP shaders are found by string and may be stripped from standalone Windows build; needs real material references + Windows player validation.
- **UNITY-010 [#45](https://github.com/MojasSimunovic/little-empires/issues/45):** 5 cottage variants / 3 townhouses and visual cardinal orientation are not implemented yet.
- **#43/#44:** road/bridge code implemented but Unity Game view and bridge placement acceptance tests still need local validation before closing issues.
- Growth, food mortality and mandatory road-worker access are **undecided product questions**. Do not change rules without user direction.

## Working conventions for Codex and agents
1. **Read this file first**, then `AGENTS.md`, `docs/ROAD_AND_BRIDGE_NEXT_STEP.md` (historical implementation brief) and current GitHub issue/PR.
2. Start by inspecting local Git status and Unity MCP/Editor; local edits may be newer than `main`. Never overwrite them automatically.
3. Canonical economic/world rules: `src/LittleEmpires.Core/`. Sync only through `tools/Install-UnityStarter.ps1 -CoreOnly`; never edit `unity/LittleEmpires/Assets/LittleEmpires/Core/` manually.
4. For C# changes build with `--warnaserror`, run smoke tests and simulator; for visuals additionally run Unity Play Mode with screenshots, Console verification and a Windows development build when relevant.
5. Check GitHub issue status against actual code before declaring a feature unimplemented or closing issues. Keep `main` stable while draft PR #46 is reviewed.
