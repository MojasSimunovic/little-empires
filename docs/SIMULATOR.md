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

To run the **happiness and recovery** scenario, use:

```bash
dotnet run --project src/LittleEmpires.Simulator -- happiness 8
```

To run the **Cottage → Townhouse upgrade** scenario, use:

```bash
dotnet run --project src/LittleEmpires.Simulator -- upgrade 8
```

The GitHub Actions workflow builds and executes **all four** scenarios even if you cannot install .NET locally. Open the latest `C# Core CI` run, select the job, and expand any **Run eight-season ... scenario** step to view the printed report.

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

## Happiness and recovery scenario (LE-017)

- Starts with **8 residents**, **75 happiness**, and **0 food**. The building plan matches the growth scenario.
- The initial food shortages lower happiness; even after food production recovers, immigration stays paused until happiness reaches 50.
- When happiness is 50–69, at most **one** resident can immigrate per season; at 70+, up to **two** can immigrate, subject to food and housing.
- This is a first-pass mechanic with fixed per-season adjustments, not a simulation of individual residents.

| Turn | Season | Food shortage | End happiness | End population | New residents |
|---:|---|---:|---:|---:|---:|
| 1 | Spring, Year 1 | 8 | 64 | 8 | 0 |
| 2 | Summer, Year 1 | 3 | 53 | 8 | 0 |
| 3 | Autumn, Year 1 | 3 | 42 | 8 | 0 |
| 4 | Winter, Year 1 | 0 | 45 | 8 | 0 |
| 5 | Spring, Year 2 | 0 | 48 | 8 | 0 |
| 6 | Summer, Year 2 | 0 | 51 | 9 | 1 |
| 7 | Autumn, Year 2 | 0 | 54 | 10 | 1 |
| 8 | Winter, Year 2 | 0 | 57 | 11 | 1 |

Expected total food shortage: **14**. Expected final food: **27**, population **11**, happiness **57**.

### Happiness rules

- Starts at **70** by default (the recovery scenario deliberately starts at 75).
- A season with enough food: **+2**; any food shortage: **−12**.
- At least one unoccupied completed housing slot: **+1**; no available housing: **−1**.
- Happiness is clamped to **0–100** and calculated **after** construction completes but **before** immigration.
- A food shortage always blocks immigration, regardless of happiness. Food and housing constraints continue to apply.
- The penalty is currently fixed per shortage season, not proportional to missing food.

## Cottage-to-Townhouse upgrade scenario (LE-018)

- Starts with **8 residents**, **70 happiness**, **100 wood**, **40 stone**, **60 food**, **100 coins**.
- Builds **Cottage + Fishery** in turn 1, **Farm** in turn 2.
- On turn 4, pays **24 wood, 10 stone, 20 coins** to upgrade the existing Cottage (building index 0).
- The upgrade takes **two turns**: the Cottage remains available with 6 slots on turn 4, then becomes a Townhouse with **12 slots** on turn 5.
- Immigration immediately uses the expanded capacity when the upgrade completes; it still respects food surplus and happiness.
- No new building is created: the original building changes type in place.

| Turn | Season | Housing cap | Population | Happiness | Upgrade |
|---:|---|---:|---:|---:|---|
| 1 | Spring, Year 1 | 14 | 10 | 73 | Cottage completed |
| 2 | Summer, Year 1 | 14 | 12 | 76 | — |
| 3 | Autumn, Year 1 | 14 | 14 | 79 | — |
| 4 | Winter, Year 1 | 14 | 14 | 80 | Upgrade started (1/2) |
| 5 | Spring, Year 2 | 20 | 16 | 83 | Townhouse completed (2/2) |
| 6 | Summer, Year 2 | 20 | 18 | 86 | — |
| 7 | Autumn, Year 2 | 20 | 20 | 89 | — |
| 8 | Winter, Year 2 | 20 | 20 | 90 | — |

Expected final resources: **21 wood, 17 stone, 28 food, 50 coins**. No food shortages; 12 total immigrants; one Cottage upgraded to Townhouse. This is a deterministic benchmark, not a final balance.

## Current limitations

- No map or terrain, worker assignments, housing quality tiers beyond Townhouse, services, trade, or AI rivals.
- Food shortages are reported but do not reduce population. Food security uses a simple one-surplus-food-per-newcomer rule, not a forecast of next season's needs.
- Building production and consumption are currently constant across seasons.
- The scenario is scripted rather than an autonomous economic AI. Upgrade orders currently target a building index rather than a persistent world-object ID.
- Starting values are provisional; use reports to tune balance after playtesting.

For architecture and building costs see [ARCHITECTURE.md](ARCHITECTURE.md) and [BUILDINGS.md](BUILDINGS.md).
