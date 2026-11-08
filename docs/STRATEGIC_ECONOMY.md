# Little Empires — Strategic Economy rules (LE-022 through LE-025)

**C# is authoritative.** The future Unity view reads the simulation and sends commands. Do not independently implement production, taxes or trade in Unity.

## Economy modes and backward compatibility

`Settlement` has an explicit `EconomyMode`:

- **Classic** (default): preserved for the original economic scenarios. Production does not require workers or upkeep; taxation is disabled. Old balance tests continue to run unchanged.
- **Strategic**: intended for the real game. Production requires staffing, operating buildings pay upkeep, tax policy generates income and affects happiness. Construct using `new Settlement(population: 8, economyMode: EconomyMode.Strategic)` or `WorldGameSession.NewStrategicIsland(seed)`.

The optional `IslandMap` parameter on `TurnEngine.EndTurn(state, settlement, island)` enables spatial processing bonuses. Without an island, normal unboosted production works, retaining the earlier console scenarios.

## Building catalogue, workforce and upkeep

| Building | Cost to construct | Time | Worker demand | Operating coins / season | Base output or effect |
|---|---|---:|---:|---:|---|
| Lumber Camp | 15 wood + 10 coins | 2 | 3 | 2 | +6 wood |
| Fishery | 12 wood + 3 stone + 8 coins | 1 | 3 | 2 | +5 food |
| Quarry | 20 wood + 15 coins | 2 | 4 | 3 | +4 stone |
| Farm | 25 wood + 5 stone + 12 coins | 2 | 4 | 3 | +9 food |
| Cottage | 18 wood + 5 stone + 10 coins | 1 | 0 | 0 | 6 housing slots |
| Townhouse | Cottage upgrade: 24 wood + 10 stone + 20 coins | 2 (upgrade) | 0 | 0 | 12 housing slots total |
| **Sawmill (Town)** | 35 wood + 15 stone + 30 coins | 2 | 2 | 4 | +50% to linked Lumber Camps |
| **Food Processing Workshop (Town)** | 40 wood + 20 stone + 40 coins | 3 | 3 | 5 | +25% to linked Farms / Fisheries |
| **Stoneworks (Town)** | 35 wood + 20 stone + 45 coins | 2 | 2 | 4 | +50% to linked Quarries |
| **Trading Harbor (Town)** | 45 wood + 30 stone + 100 coins | 3 | 0 | 0 | Shoreline trade with 120-unit seasonal capacity |
| **Marketplace** | 25 wood + 10 stone + 20 coins | 2 | 0 | 0 | Buy/sell resources, 30-unit seasonal capacity |

Worker demand and upkeep are **provisional**. Market trading is a player action and does not yet require staffed vendors, but the Marketplace must be complete and connected. The settlement’s entire population is eligible for work in this first version; an age/labor-force split is future work.

### Staffing
- Only **completed** buildings are eligible for staffing.
- Up to one worker per resident may be assigned; a person cannot staff two buildings.
- Buildings are considered in descending `Building.WorkPriority`, breaking ties by stable construction order. Priorities range -10 to +10, changeable via `Settlement.SetWorkPriority(index, priority)`.
- If there are fewer workers than required, production is proportional and **rounded down** per resource. Example: a Lumber Camp with 1 of its 3 workers generates **2 wood** rather than 6.
- A partially staffed building pays its **full fixed upkeep**. If it cannot afford the upkeep from the current coin stockpile, it receives zero workers and produces nothing. Idle residents remain available for later buildings in the allocation order.
- Production boosters (Mills and Processing Plants) require at least one worker and paid upkeep before their bonus applies in Strategic mode.

## Processing and road connectivity

The Sawmill (existing `lumber-mill` ID) provides **+50% wood** to Lumber Camps; the Food Processing Workshop (existing `food-processor` ID) provides **+25% food** to Farms/Fisheries; Stoneworks provides **+50% stone** to Quarries.

For any bonus:
- Both producer and processor must be **complete and operational**.
- Both need a valid `GridPosition` and must be on the **same road-connected component**, as tested by `IslandRoadNetwork.AreBuildingsConnected`.
- Their Manhattan distance must be **6 tiles or less**.
- Each eligible producer receives **at most one** bonus for a given resource, even with multiple eligible processors.
- Bonuses apply **after** partial staffing output, rounded down. Example: a fully staffed Farm gives 9 + floor(9 × 25%) = **11 food** per season; a fully staffed Fishery gives 5 + floor(5 × 25%) = **6 food**.
- Processor plants do **not** yet consume the underlying Wood/Food. This is an early productivity bonus, not a separate item-conversion chain. More detailed resource processing will be introduced after playtesting.

## Taxation and happiness

| Tax policy | Tax coins / resident / season | Happiness / season |
|---|---|---:|
| Low | 0.5 (floor total) | +2 |
| Normal | 1 | 0 |
| High | 1.5 (floor total) | −4 |
| Very High | 2 | −8 |

Tax collection is based on the **starting population** of the season; immigrants pay from the following turn. Income uses integer arithmetic for reproducibility. `Settlement.SetTaxPolicy(TaxPolicy.High)` changes the current policy without an action-point cost. These adjustments add to the existing food/housing happiness effects, with happiness clamped to 0–100.

There are no tax revolts, city services or wealth tiers yet. High taxation can slow immigration through the existing happiness thresholds, but will not directly remove citizens in this slice.

## Marketplace buy/sell

A Marketplace is only usable when its construction is **complete** and it has a **road connection to at least one completed residential building** (Cottage/Townhouse). It needs a spatial island and a stable building index to trade.

| Resource | Market buys from player (sell price) | Market sells to player (buy price) |
|---|---:|---:|
| Wood | 3 coins | 5 coins |
| Stone | 5 coins | 8 coins |
| Food | 2 coins | 4 coins |

- `MarketplaceTrading.Execute(state, settlement, island, marketplaceIndex, direction, resource, units)` uses explicit success/failure results.
- One Marketplace handles at most **30 total units per season** (combined buys and sells), reset during `EndTurn`.
- One Trading Harbor handles **120 total units per season**, resets on `EndTurn`, uses the same goods/prices and road-to-residence requirement, and must be constructed on shoreline after reaching Town tier. No distant island or ship simulation is needed.
- An attempt to sell requires enough stock; buying requires enough coins. Invalid quantities, unsupported resources, incomplete/disconnected markets, capacity violations and arithmetic overflow are **rejected without any changes** to inventory or quotas.
- Spread prevents profitable immediate buy→sell loops.
- Prices and seasonal capacity are fixed for now. World supply/demand, import tariffs, trade routes, shipping, market stalls and regional prices come later.

## Seasonal resolution (Strategic)

1. Snapshot starting residents and resources.
2. Collect **taxes** from starting residents.
3. Allocate **workers**, paying operating costs for active buildings.
4. Resolve building **production** and at-most-once road-linked processing bonuses.
5. Consume **food** for starting residents; record shortages.
6. Advance **construction and housing upgrades**.
7. Resolve **happiness**, including tax-policy effect.
8. Handle **immigration** (new residents eat and pay taxes next season).
9. Advance season/year; reset Marketplace transaction quotas.
10. Return `TurnSummary` with gross production, tax income, upkeep, workforce, resource deltas, happiness and construction events.

Buildings that finish during a season start producing next season. Housing completed during a season can attract immigrants in that same season, after current residents have eaten.

## Unity handoff

`WorldGameSession` offers engine-independent game commands and a read-only `WorldSnapshot` containing terrain, roads, buildings, resources and economy state. A Unity scene should render `WorldSnapshot`, use `PreviewBuilding` for placement previews, then send `PlaceBuilding`, `PlaceDirtRoad`, `UpgradeStoneRoad`, `Trade`, `ChangeTaxes`, `ChangeWorkPriority`, and `EndSeason` commands.

**Still required before shipping Unity integration:** Unity-compatible target framework/assembly packaging, explicit scene/prefab bindings, save versioning, content data configuration, proper UI validation, and playtesting. Our C# project is currently `net8.0`; that does not guarantee direct compatibility with the Unity runtime.

## Running regression checks

```bash
dotnet run --project tests/LittleEmpires.SmokeTests
dotnet run --project src/LittleEmpires.Simulator -- strategic 8
```

The GitHub Actions workflow runs these with existing four legacy scenario checks and the separate JavaScript prototype tests. The old Three.js browser experience is **not** automatically changed, and its saved games are preserved.
