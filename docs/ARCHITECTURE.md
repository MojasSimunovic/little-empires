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
- Strategic actions, diplomacy, Council, consumption and trade will be introduced in later tickets.

## Layout
- `src/LittleEmpires.Core`: Unity-independent C# simulation.
- `tests/LittleEmpires.SmokeTests`: dependency-free executable assertions.
- Future Unity project: `unity/LittleEmpires` (when editor becomes available).

## Local verification (requires approved .NET 8 SDK)
```bash
dotnet run --project tests/LittleEmpires.SmokeTests
```

These files have not yet been compiled or executed in Unity; integration is a future milestone.
