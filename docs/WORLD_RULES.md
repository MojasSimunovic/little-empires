# Island world rules (LE-020)

## Why the terrain belongs in the C# simulation

The player sees meshes in Unity (or in today's temporary Three.js demo), but **C# decides** whether a tile can be occupied by a building. Unity should render `IslandMap` and call `BuildingPlacementRules.Evaluate(...)` for placement previews. Clicks then call `Settlement.TryStartConstruction(state, island, position, buildingId)`.

The existing non-spatial `Settlement.TryStartConstruction(state, buildingId)` overload remains **only for legacy demos and smoke tests**; new world gameplay should always use the spatial overload.

## Logical model

- `GridPosition(X, Z)`: integer tile coordinates; deliberately independent of Unity's `Vector3`.
- `TerrainKind`: `Water`, `Grassland`, `FertileLand`, `Forest`, `Mountain`.
- `IslandTile`: location + immutable terrain.
- `IslandMap`: rectangular grid, shoreline queries, orthogonal neighbors, occupied-building references.
- `IslandGenerator.Generate(seed, width = 13, height = 13)`: seeded deterministic prototype terrain. Width and height must be between 9 and 64.
- `BuildingDefinition.SiteRequirement`: catalog-controlled placement rule. `BuildingPlacementRules.Evaluate(...)` returns `PlacementFailure`, suitable for UI error messages and red/green previews.

## Terrain restrictions

| Building | Terrain requirement | Explanation |
|---|---|---|
| Quarry | Mountain | Occupies a mountain plot |
| Lumber Camp | Forest edge | Flat plot immediately north/south/east/west of at least one forest |
| Fishery | Shoreline | Flat plot next to water (4-way adjacency) |
| Farm | Fertile land | Fertile plots only, a subset of flat land |
| Cottage | Flat land | Grassland or fertile land |
| Townhouse | Upgrade-only | Cannot be directly placed; upgrades its existing Cottage |

All building sites must be inside the map, non-water, and not already occupied by another building. Placing on forest tiles is not yet a deforestation action; forestry uses neighboring land. Dirt and stone roads will have separate spatial rules in LE-021.

## Generation guarantees

Every seeded island contains a *usable* mountain for a Quarry, forest-edge site for a Lumber Camp, shoreline site for a Fishery, fertile site for a Farm, and flat land for a Cottage. Water surrounds the island. Tests verify these guarantees for at least 120 generated maps of differing dimensions.

These are intentionally deterministic **gameplay guarantees**; they are not exhaustive procedural island generation. Future passes will add larger archipelagos, noise-based terrain height, forest regeneration, rivers, and accessibility checks for roads. We should not generate unplayable starting islands.

## Usage

```csharp
using LittleEmpires.Core;

var island = IslandGenerator.Generate(seed: 42);
var resources = new GameState();
var town = new Settlement(population: 8);
var quarry = BuildingCatalog.Get(BuildingCatalog.QuarryId);

var chosenTile = island.Tiles.First(tile =>
    BuildingPlacementRules.Evaluate(island, tile.Position, quarry).Allowed);

var preview = BuildingPlacementRules.Evaluate(island, chosenTile.Position, quarry);
// preview.Failure gives the reason, when a tile is invalid.
if (preview.Allowed)
{
    var constructed = town.TryStartConstruction(
        resources, island, chosenTile.Position, BuildingCatalog.QuarryId);
}
```

Construction costs are deducted up front **only if** both location and inventory are valid; the same `Building` instance is stored by the settlement and the map occupancy layer. In-place upgrades retain the building's grid position, avoiding duplicate occupancy.

## Important limitations

- Existing C# simulation scenarios are not silently converted to spatial gameplay; the new API is opt-in to preserve their test expectations.
- Spatial commands are single-threaded; future multiplayer/server code will need transactional command processing.
- Roads, terrain modification, destructible forests, working radius, visual heights, navigation and Unity meshes are not yet built.
- A future save format should store a world/schema version, seed, building positions, costs paid, construction/upgrade progress, resources, and population state. Do **not** silently overwrite existing Three.js browser saves. We will write a separate Unity save migration strategy.
- The current C# project targets .NET 8. Unity-compatible assembly packaging and API compatibility must be explicitly addressed in **LE-026**, rather than assuming a .NET 8 assembly can be dropped into Unity unchanged.
