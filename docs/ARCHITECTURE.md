# Little Empires — Architecture (initial)

## Intent
Unity 6 is the target engine. The core simulation is written in plain C# without UnityEngine references so we can develop and test game rules before installing Unity.

## Core rules (first slice)
- A turn represents one season: Spring → Summer → Autumn → Winter → next year.
- Construction is limited by resources, **not** by strategic action points.
- Building costs are paid upfront.
- Construction advances once per season.
- Newly completed buildings start producing in the **following** season.
- Production from completed buildings is resolved before advancing construction.
- The first slice uses wood, stone, food, and coins.
- The turn pipeline is: existing-building production → existing residents consume food → construction and upgrade progress → happiness update → immigration → calendar advancement → turn summary.
- Food consumption is one food per resident per season. Food shortages are reported but do not change population yet.
- Starting residents are assumed housed; new housing capacity comes from completed Cottages. Each Cottage adds 6 housing slots.
- Completed Cottages can be upgraded **in place** to Townhouses (12 housing slots total). Upgrades take two seasons, are paid upfront, and retain the original housing capacity until completion. Townhouses cannot be directly constructed.
- Scripted upgrades use the current building list index as a temporary identifier; future world entities will have persistent IDs.
- Up to 2 immigrants arrive after construction each season if there is spare housing, no food shortage, sufficient food surplus, and adequate happiness. They eat beginning the following season.
- Happiness (0–100, starts at 70) rises modestly with food security and spare housing, and falls sharply after food shortages. Immigration requires happiness ≥50; 50–69 allows at most 1 newcomer, 70+ allows at most 2.
- Building definitions and provisional balance values live in `BuildingCatalog` (see [BUILDINGS.md](BUILDINGS.md)).
- Spatial terrain, shoreline/forest adjacency, placement checks and occupancy are now available via `IslandMap`, `IslandGenerator` and `BuildingPlacementRules` (see [WORLD_RULES.md](WORLD_RULES.md)). Older simulation scenarios still use the non-spatial constructor overload.
- Roads are represented by `IslandMap.Roads`: free dirt-road placement, paid stone-road upgrades, separate occupancy and four-way connectivity to buildings; see [ROADS.md](ROADS.md).
- Road connections currently have no production effect; processing, taxes, trade, workforce, strategic actions, diplomacy and Council follow; see [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md).
- Unity integration is a separate compatibility milestone. The current core targets .NET 8 and must not be assumed Unity-assembly compatible yet.

## Layout
- `src/LittleEmpires.Core`: Unity-independent C# simulation.
- `tests/LittleEmpires.SmokeTests`: dependency-free executable assertions.
- `src/LittleEmpires.Simulator`: console-based multi-season balancing scenario, powered by the same `TurnEngine` (see [SIMULATOR.md](SIMULATOR.md)).
- Future Unity project: `unity/LittleEmpires` (when editor becomes available).

## Local verification (requires approved .NET 8 SDK)
```bash
dotnet run --project tests/LittleEmpires.SmokeTests
```

These files have not yet been compiled or executed in Unity; integration is a future milestone.
