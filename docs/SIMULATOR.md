# Settlement Economy Simulator (LE-015)

The console simulator runs the **same** `TurnEngine` as the future Unity game. It is a deterministic balancing sandbox, not a separate game implementation.

## Run locally (approved .NET 8 SDK)

```bash
dotnet run --project src/LittleEmpires.Simulator
```

The default is **8 seasons** (two years). Optionally pass a number of seasons from 1 to 80:

```bash
dotnet run --project src/LittleEmpires.Simulator -- 12
```

To run the new **housing and immigration** scenario, use:

```bash
dotnet run --project src/LittleEmpires.Simulator -- growth 8
```

The GitHub Actions workflow builds and executes **both** scenarios even if you cannot install .NET locally. Open the latest `C# Core CI` run, select the job, and expand **Run eight-season economy scenario** or **Run eight-season housing and immigration scenario** to view the printed report.

## Default scenario

- Start at **Spring, Year 1** with **8 residents**.
- Initial stockpile: **80 wood, 25 stone, 12 food, 100 coins**.
- Build a **Fishery** in turn 1 (Spring), **Farm** in turn 2 (Summer), **Lumber Camp** in turn 4 (Winter), and **Quarry** in turn 5 (Spring, Year 2).
- Orders are attempted at the start of each turn; costs are paid immediately.
- A building completes at the end of its final construction turn and produces beginning with the next turn.
- Existing buildings produce before the population eats one food per resident.
- A failed construction order is reported and is **not automatically retried**.

## Expected eight-season outcome

| Turn | Season | End food | Food shortage |
|---:|---|---:|---:|
| 1 | Spring, Year 1 | 4 | 0 |
| 2 | Summer, Year 1 | 1 | 0 |
| 3 | Autumn, Year 1 | 0 | 2 |
| 4 | Winter, Year 1 | 6 | 0 |
| 5 | Spring, Year 2 | 12 | 0 |
| 6 | Summer, Year 2 | 18 | 0 |
| 7 | Autumn, Year 2 | 24 | 0 |
| 8 | Winter, Year 2 | 30 | 0 |

Expected final stockpile: **26 wood, 25 stone, 30 food, 55 coins**. Expected final date: **Spring, Year 3**. These values are asserted in the smoke tests.

## Housing and immigration scenario (LE-016)

- Starts with **8 residents / 8 housing slots**, **30 food**, **80 wood**, **25 stone**, **100 coins**.
- Builds a **Cottage** and **Fishery** on turn 1, then a **Farm** on turn 2.
- Cottage adds **6 housing slots** when complete, bringing the cap to **14 residents**.
- Up to **2 new residents** arrive each season, provided there's housing and at least one food surplus per immigrant after current residents eat. New residents eat starting next season.
- The existing default economy scenario has **no extra housing**, so its population stays at 8 and its expected balances are unchanged.

| Turn | Season | End population | Housing cap | End food | Immigrants |
|---:|---|---:|---:|---:|---:|
| 1 | Spring, Year 1 | 10 | 14 | 22 | 2 |
| 2 | Summer, Year 1 | 12 | 14 | 17 | 2 |
| 3 | Autumn, Year 1 | 14 | 14 | 10 | 2 |
| 4 | Winter, Year 1 | 14 | 14 | 10 | 0 |
| 5 | Spring, Year 2 | 14 | 14 | 10 | 0 |
| 6 | Summer, Year 2 | 14 | 14 | 10 | 0 |
| 7 | Autumn, Year 2 | 14 | 14 | 10 | 0 |
| 8 | Winter, Year 2 | 14 | 14 | 10 | 0 |

Expected final stockpile: **25 wood, 12 stone, 10 food, 70 coins**. Six immigrants in total; no food shortages. These are deterministic, provisional test expectations.

## Current limitations

- No map or terrain, worker assignments, advanced housing upgrades, trade, or AI rivals.
- Food shortages are reported but do not reduce population. Food security uses a simple one-surplus-food-per-newcomer rule, not a forecast of next season's needs.
- Building production and consumption are currently constant across seasons.
- The scenario is scripted rather than an autonomous economic AI.
- Starting values are provisional; use reports to tune balance after playtesting.

For architecture and building costs see [ARCHITECTURE.md](ARCHITECTURE.md) and [BUILDINGS.md](BUILDINGS.md).
