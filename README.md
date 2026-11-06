# Little Empires

A cozy, seasonal-turn, island-building strategy game in development.

**Target engine:** Unity 6 / C#. The production game logic lives in `src/LittleEmpires.Core` and runs independently from Unity so it can be tested while the Unity editor is not yet installed.

## Try our interactive island

We now have a **Three.js playable prototype** in [`demo/`](demo/). It supports procedural 3D island terrain, camera controls, building placement, resource production, seasonal turns, housing, happiness, immigration, and Cottage-to-Townhouse upgrades.

[Open the demo's instructions](demo/README.md).

The visual browser demo uses a JavaScript **mirror** of the C# economy for experimentation. It does **not** run the C# assembly. The C# simulation remains authoritative.

## Run checks

```bash
dotnet run --project tests/LittleEmpires.SmokeTests
node demo/test-engine.mjs
```

Or visit GitHub Actions to see automated .NET and JavaScript checks.

## Explore

- [C# world and placement rules](docs/WORLD_RULES.md)
- [C# dirt and stone roads](docs/ROADS.md)
- [C#-first development roadmap](docs/DEVELOPMENT_ROADMAP.md)
- [Strategic economy: industry, taxes, workforce and Marketplace](docs/STRATEGIC_ECONOMY.md)
- [Unity 6 integration handoff](docs/UNITY_HANDOFF.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Building catalogue](docs/BUILDINGS.md)
- [Seasonal simulator](docs/SIMULATOR.md)
- [Playable Three.js island prototype](demo/)
- [Development issues](../../issues)
