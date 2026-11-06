# Roads and connectivity (LE-021)

All road rules are in the Unity-independent C# simulation. Unity will render the terrain and road meshes and call these APIs in response to player input; it must not contain a parallel road economy.

## Two road surfaces

| Road | Placement | Upfront cost | Construction time | Connectivity |
|---|---|---|---|---|
| Dirt | Any free Grassland or FertileLand tile | **Free** | Immediate | Four-way |
| Stone | Upgrade an existing Dirt segment | **2 stone per segment** | Immediate | Four-way |

The cost is provisional. Road construction and road upgrades do **not** consume seasonal strategic action points. No road upkeep or transport-speed multiplier exists yet; roads only establish a network.

A road tile cannot overlap an existing building. Buildings also cannot be constructed on an existing road. No roads on water, forest or mountain tiles until we explicitly add bridges, clearing or mountain passes in later features. Dirt roads may replace potential fertile farmland, which creates an early spatial tradeoff.

## Data/API

Every `IslandMap` owns a single `IslandRoadNetwork` in `island.Roads`. Road surfaces are available to rendering code as a read-only map, `island.Roads.Segments`, mapping `GridPosition` to `RoadSurface.Dirt` or `RoadSurface.Stone`.

```csharp
using LittleEmpires.Core;

var island = IslandGenerator.Generate(seed: 42);
var state = new GameState(stone: 12);

var position = new GridPosition(6, 7);

// Preview without changing the island.
RoadActionResult preview = island.Roads.EvaluateDirtPlacement(position);
if (preview.Succeeded)
    island.Roads.PlaceDirt(position); // free

// Preview the upgrade, including the player's current stone inventory.
RoadActionResult upgradePreview = island.Roads.EvaluateStoneUpgrade(state, position);
if (upgradePreview.Succeeded)
    island.Roads.UpgradeToStone(state, position); // charges 2 stone
```

Each action returns a `RoadActionResult`. When `Succeeded` is false, `Failure` explains the problem: out of bounds, water, non-flat land, building occupied, road already present, road missing, already stone, or insufficient stone. Invalid placement and upgrade attempts never spend resources.

## Connectivity queries

`IslandRoadNetwork` exposes:

- `HasRoad(position)`: tile has a road.
- `ConnectedComponent(startingRoad)`: read-only snapshot of its four-way-connected road component; empty if missing.
- `AreConnected(firstRoad, secondRoad)`: true if both road tiles are in the same component.
- `IsBuildingRoadConnected(buildingPosition)`: a map-occupied building has at least one adjacent road.
- `AreBuildingsConnected(firstBuilding, secondBuilding)`: two map-occupied buildings touch a common connected road component.

Roads may form branches and loops. A disconnected road segment is valid, but it does not connect two buildings. **Diagonal contact is not a connection.** Stone and dirt road segments are interchangeable for connectivity: upgrading a segment does not break the network.

The building connection API includes construction sites for now; future production bonuses may additionally require completed operational buildings.

## Example: connecting a Quarry

```csharp
var island = IslandGenerator.Generate(seed: 42);
var state = new GameState(wood: 100, stone: 30, food: 100, coins: 100);
var settlement = new Settlement(population: 8);

var cx = island.Width / 2;
var cz = island.Height / 2;
var home = new GridPosition(cx, cz);
var quarry = new GridPosition(cx + 2, cz - 1);

settlement.TryStartConstruction(state, island, home, BuildingCatalog.CottageId);
settlement.TryStartConstruction(state, island, quarry, BuildingCatalog.QuarryId);

island.Roads.PlaceDirt(new GridPosition(cx + 1, cz));
island.Roads.PlaceDirt(new GridPosition(cx + 1, cz - 1));

bool connected = island.Roads.AreBuildingsConnected(home, quarry); // true
```

The generated map guarantees a flat approach to its primary mountain site.

## Integration decisions

- Unity UI should render the `RoadActionResult.Failure` as useful placement feedback.
- We do **not** mandate road connectivity for basic production yet; that will come after the first processing buildings and workforce balancing.
- The future Marketplace and ports will use these queries to check accessibility.
- Persistence/save-load, road drawing strokes, bridges, route priority, faster stone roads and logistics multipliers are later steps.
- The older Three.js experiment has its own data model; it isn't automatically updated by these C# changes. Do not silently invalidate or overwrite existing browser saves.
