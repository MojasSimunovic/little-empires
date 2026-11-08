# Starter Building Catalogue (LE-014)

This is an **initial balance proposal**, not a final economy. The catalogue lives in `src/LittleEmpires.Core/BuildingCatalog.cs`; the gameplay/UI layer can use a stable building ID to begin construction.

| Building | ID | Upfront cost | Seasons to construct | Effect |
|---|---|---|---:|---|
| Lumber Camp | `lumber-camp` | 15 wood + 10 coins | 2 | +6 wood |
| Fishery | `fishery` | 12 wood + 3 stone + 8 coins | 1 | +5 food |
| Quarry | `quarry` | 20 wood + 15 coins | 2 | +4 stone |
| Farm | `farm` | 25 wood + 5 stone + 12 coins | 2 | +9 food |
| Village Hall | `village-hall` | 35 wood + 15 stone + 30 coins | 1 | One-per-settlement civic administration and tax panel |
| Town Hall | `town-hall` | Upgrade-only: 60 wood + 25 stone + 300 coins, minimum 50 population | 2-season upgrade | Upgraded civic hall; same administrative functions and position |
| Cottage | `cottage` | 18 wood + 5 stone + 10 coins | 1 | +6 housing slots |
| Townhouse | `townhouse` | Upgrade-only; see below | 2-season upgrade | 12 total housing slots (replaces Cottage) |
| Sawmill (Town) | `lumber-mill` | 35 wood + 15 stone + 30 coins | 2 | +50% connected Lumber Camp output |
| Food Processing Workshop (Town) | `food-processor` | 40 wood + 20 stone + 40 coins | 3 | +25% connected Farm/Fishery output |
| Stoneworks (Town) | `stoneworks` | 35 wood + 20 stone + 45 coins | 2 | +50% connected Quarry output |
| Trading Harbor (Town) | `trading-harbor` | 45 wood + 30 stone + 100 coins | 3 | Shoreline trade post, 120 units/season |
| Marketplace | `marketplace` | 25 wood + 10 stone + 20 coins | 2 | Village trade post, 30 units/season |

## Gameplay reasoning

- **Fishery:** relatively cheap, fast food security for a new settlement.
- **Farm:** slower and more expensive to establish, higher sustained food production.
- **Lumber Camp:** renewable construction material once built.
- **Quarry:** provides stone for expanding settlements.
- **Village Hall:** player-placed one-tile settlement centre; one per settlement; finance, tax controls and settlement statistics. No housing, extra tax bonus or resource production.
- **Town Hall:** same one-tile civic building upgraded in place after 50 population and a substantial resource cost; keeps existing taxation policy and ongoing seasonal collection.
- **Cottage:** expands housing capacity; does not produce resources.
- **Townhouse:** denser housing that replaces an existing completed Cottage in place.
- **Sawmill:** original `lumber-mill` mechanic and ID; renamed and locked until Town tier.
- **Food Processing Workshop:** original `food-processor` mechanic and ID; renamed and locked until Town tier.
- **Stoneworks:** Town-tier, staffed, road-connected, nonstacking +50% stone output for Quarries.
- **Marketplace:** Village trading with 30-unit/season capacity.
- **Trading Harbor:** Town-only, build on level shoreline, road-connected to housing for trading; 120-unit/season capacity using existing buy/sell spreads. Foreign nations are abstracted.

## Current simulation rules
- Building construction **does not use strategic action points**.
- Building costs are charged in full when construction starts.
- In Classic mode, buildings produce before residents consume one food each; Strategic mode also collects taxes, allocates workers and charges upkeep first.
- Construction progresses later in the turn. A building that finishes construction during a turn starts producing **next** turn.
- If there is not enough food, inventory stops at zero and the turn report records the deficit.
- The founding residents start with exactly enough housing for themselves; no spare capacity.
- Cottages add six housing slots **only when construction completes**. Immigration happens after construction progress, allowing the first arrivals in the completion season.
- A completed Village Hall can be upgraded to a Town Hall, using the SAME upgrade pipeline as housing. The 50-resident gate is enforced before charging resources. Only one civic hall may exist at a time. The Village Hall is currently the recommended first player action, not a hard blocker on placing other buildings.
- In the real Strategic game, **20 residents** are required to upgrade a Cottage; this is checked before charging. Classic legacy scenarios skip progression gates.
- **Town tier is earned only when the Town Hall upgrade finishes**, not by reaching 50 residents or beginning the upgrade. All four Town industries are visible but locked until then. The C# placement and construction APIs reject premature building without spending resources.
- A completed Cottage can be upgraded to a Townhouse for **24 wood, 10 stone and 20 coins**. The upgrade takes **2 seasons** and costs are paid upfront. A Cottage keeps its 6 slots while upgrading; on completion it becomes a Townhouse with 12 slots (**+6 net**, not +12). It cannot be upgraded again.
- Townhouses are **not directly buildable**; their definition is a target for the upgrade system. Upgrade orders use the building index for now, until world-object IDs exist.
- Immigration brings at most two residents per season, limited by free housing and food remaining after feeding existing residents (at least one surplus food per arrival). Immigration does not consume food; arrivals eat from the following season.
- No immigration during food shortages; shortages do not reduce population yet.
- Happiness affects immigration: below 50 none, 50–69 at most 1, 70+ at most 2; shortages block it entirely.
- C# spatial terrain and road adjacency rules are implemented. Strategic mode activates staffing, upkeep, taxation, non-stacking industrial bonuses and Marketplace trading; see [STRATEGIC_ECONOMY.md](STRATEGIC_ECONOMY.md).
- Storage limits, seasonal production modifiers, distinct processed goods and housing-quality demand are not yet implemented.
- The values above may change after playtesting.

## Example

```csharp
var state = new GameState();
var settlement = new Settlement(population: 5);
var engine = new TurnEngine();

if (settlement.TryStartConstruction(state, BuildingCatalog.FisheryId))
{
    // Construction is paid for immediately, then progresses on EndTurn.
}

var summary = engine.EndTurn(state, settlement);
```
