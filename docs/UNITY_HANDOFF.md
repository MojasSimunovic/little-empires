# Unity 6 handoff — Little Empires

**Status:** C# simulation integration boundary prepared. No Unity scene, prefabs, import package or Unity build has been created or tested yet.

## What is ready today

The `LittleEmpires.Core` project is independent of Unity. The Unity scene should use **one persistent `WorldGameSession`** to own the model.

```csharp
// This is engine-neutral C#, not a MonoBehaviour yet:
using LittleEmpires.Core;

var game = WorldGameSession.NewStrategicIsland(seed: 42);
var view = game.Snapshot();

foreach (var tile in view.Terrain)
{
    // Spawn a mesh or terrain tile at tile.Position using tile.Terrain.
}
```

### Intended scene/UI command mapping

| Unity interaction | Call in the simulation |
|---|---|
| Display terrain / roads / buildings | `game.Snapshot()` |
| Hover over a possible building tile | `game.PreviewBuilding(gridPosition, buildingId)` |
| Confirm building placement | `game.PlaceBuilding(gridPosition, buildingId)` |
| Draw dirt road | `game.PlaceDirtRoad(gridPosition)` |
| Upgrade dirt road to stone | `game.UpgradeStoneRoad(gridPosition)` |
| Upgrade Cottage | `game.UpgradeBuilding(buildingIndex)` |
| Change tax preset | `game.ChangeTaxes(policy)` |
| Prioritize workers | `game.ChangeWorkPriority(buildingIndex, priority)` |
| Buy/sell goods at a Marketplace | `game.Trade(marketIndex, direction, resource, units)` |
| End season | `game.EndSeason()` and refresh `Snapshot()` |

All player commands are resolved in C#. Unity only needs to render state changes, show validation failures and play animations.

## Integration checklist for your personal Windows PC

1. Install **Unity Hub and a supported Unity 6 editor**, then create a **3D URP** project.
2. Import the C# source after deciding a Unity-compatible target/API surface. **Do not simply copy the existing .NET 8 DLL** and assume it loads.
3. Confirm the Unity scripting runtime supports the features used by the core: records, pattern matching, nullable reference types, `ArgumentNullException.ThrowIfNull`, `Enum.IsDefined<T>`, and collection APIs. Refactor/polyfill unsupported APIs or multi-target the source where needed.
4. Add a scene entry point (e.g. `GameBootstrap`) that creates and holds `WorldGameSession`.
5. Use prefab IDs matching `BuildingCatalog` constants and terrain meshes matching `TerrainKind`.
6. Use orthogonal grid raycasting to convert Unity world coordinates to `GridPosition`. Use `PreviewBuilding` results for valid/invalid highlighting before placing.
7. Draw road segments using `WorldSnapshot.Roads`; let the C# road graph decide connectivity.
8. Implement HUD panels for resources, happiness, taxes, workforce, market trade, and seasonal summary from `TurnSummary`.
9. Implement robust versioned save/load separately (seed, tiles, buildings, roads, seasons, inventories, happiness, tax policy, staffing, trading quota and construction progress).
10. Compare Unity scene gameplay against existing C# smoke tests and `dotnet run --project src/LittleEmpires.Simulator -- strategic 8` before adding more complexity.

## Important versioning constraints

- The C# core currently targets **.NET 8**. Unity 6 has its own scripting runtime and API compatibility constraints. We will verify and adapt rather than promising drop-in compatibility.
- Existing Three.js browser save files are a different schema; **do not overwrite or silently migrate them**.
- `BuildingIndex` is stable only while buildings aren't demolished. Before adding demolition/real persistence, introduce stable building instance IDs.
- Runtime modifiers are currently fixed catalogue data in code; a future Unity editor could expose ScriptableObjects for presentation settings, but game balance should remain testable without Unity.
- The UI may animate construction progress, but it must never advance building construction or food inventory without calling C# simulation commands.

## What remains to be implemented in Unity

This handoff is **not a completed Unity integration**. The remaining work is the Unity project, compiler compatibility, scene hierarchy, terrain and building prefabs, isometric camera, input, tooltip previews, economic HUD, local persistence and QA. Track in [LE-026](https://github.com/MojasSimunovic/little-empires/issues/35).
