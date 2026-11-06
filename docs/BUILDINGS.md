# Starter Building Catalogue (LE-014)

This is an **initial balance proposal**, not a final economy. The catalogue lives in `src/LittleEmpires.Core/BuildingCatalog.cs`; the gameplay/UI layer can use a stable building ID to begin construction.

| Building | ID | Upfront cost | Seasons to construct | Production / season |
|---|---|---|---:|---|
| Lumber Camp | `lumber-camp` | 15 wood + 10 coins | 2 | +6 wood |
| Fishery | `fishery` | 12 wood + 3 stone + 8 coins | 1 | +5 food |
| Quarry | `quarry` | 20 wood + 15 coins | 2 | +4 stone |
| Farm | `farm` | 25 wood + 5 stone + 12 coins | 2 | +9 food |

## Gameplay reasoning

- **Fishery:** relatively cheap, fast food security for a new settlement.
- **Farm:** slower and more expensive to establish, higher sustained food production.
- **Lumber Camp:** renewable construction material once built.
- **Quarry:** provides stone for expanding settlements.

## Current simulation rules
- Building construction **does not use strategic action points**.
- Building costs are charged in full when construction starts.
- Existing buildings produce first each seasonal turn; residents then consume one food each.
- Construction progresses later in the turn. A building that finishes construction during a turn starts producing **next** turn.
- If there is not enough food, inventory stops at zero and the turn report records the deficit.
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
