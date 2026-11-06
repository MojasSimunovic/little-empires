# Starter Building Catalogue (LE-014)

This is an **initial balance proposal**, not a final economy. The catalogue lives in `src/LittleEmpires.Core/BuildingCatalog.cs`; the gameplay/UI layer can use a stable building ID to begin construction.

| Building | ID | Upfront cost | Seasons to construct | Effect |
|---|---|---|---:|---|
| Lumber Camp | `lumber-camp` | 15 wood + 10 coins | 2 | +6 wood |
| Fishery | `fishery` | 12 wood + 3 stone + 8 coins | 1 | +5 food |
| Quarry | `quarry` | 20 wood + 15 coins | 2 | +4 stone |
| Farm | `farm` | 25 wood + 5 stone + 12 coins | 2 | +9 food |
| Cottage | `cottage` | 18 wood + 5 stone + 10 coins | 1 | +6 housing slots |

## Gameplay reasoning

- **Fishery:** relatively cheap, fast food security for a new settlement.
- **Farm:** slower and more expensive to establish, higher sustained food production.
- **Lumber Camp:** renewable construction material once built.
- **Quarry:** provides stone for expanding settlements.
- **Cottage:** expands housing capacity; does not produce resources.

## Current simulation rules
- Building construction **does not use strategic action points**.
- Building costs are charged in full when construction starts.
- Existing buildings produce first each seasonal turn; residents then consume one food each.
- Construction progresses later in the turn. A building that finishes construction during a turn starts producing **next** turn.
- If there is not enough food, inventory stops at zero and the turn report records the deficit.
- The founding residents start with exactly enough housing for themselves; no spare capacity.
- Cottages add six housing slots **only when construction completes**. Immigration happens after construction progress, allowing the first arrivals in the completion season.
- Immigration brings at most two residents per season, limited by free housing and food remaining after feeding existing residents (at least one surplus food per arrival). Immigration does not consume food; arrivals eat from the following season.
- No immigration during food shortages; shortages do not reduce population yet.
- Terrain, adjacency, workers, upkeep, storage limits, seasonal modifiers and population happiness are not yet implemented.
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
