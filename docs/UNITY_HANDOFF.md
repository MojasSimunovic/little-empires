# Unity 6 handoff — Little Empires

**Status:** The shared C# 9 / .NET Standard 2.1 compatibility build is green in GitHub Actions. A procedural Unity starter scene and Windows installer are now committed at [unity/GETTING_STARTED_WINDOWS.md](../unity/GETTING_STARTED_WINDOWS.md). Real Unity Editor compilation and visual QA on Windows are still pending.

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

1. Install **Unity Hub and Unity 6.3 LTS**, then create a **Universal 3D (URP)** project.
2. Clone the repository and run `tools/Install-UnityStarter.ps1 -UnityProject "C:\\Dev\\LittleEmpires"` on Windows, passing your actual Unity project path.
3. The script copies the **authoritative C# source** into an isolated assembly plus a runtime scene view, camera, and editor menu. `.NET Standard 2.1` compilation with `C# 9` is verified in GitHub Actions; actual Editor compilation still requires testing on Windows.
4. In Unity select **Little Empires > Create Starter Island Scene**. Press Play; `GameBootstrap` creates the live `WorldGameSession` and renders its terrain from `WorldSnapshot`.
5. Use prefab IDs matching `BuildingCatalog` constants and terrain meshes matching `TerrainKind`.
6. Use orthogonal grid raycasting to convert Unity world coordinates to `GridPosition`. Use `PreviewBuilding` results for valid/invalid highlighting before placing.
7. Draw road segments using `WorldSnapshot.Roads`; let the C# road graph decide connectivity.
8. Implement HUD panels for resources, happiness, taxes, workforce, market trade, and seasonal summary from `TurnSummary`.
9. Implement robust versioned save/load separately (seed, tiles, buildings, roads, seasons, inventories, happiness, tax policy, staffing, trading quota and construction progress).
10. Compare Unity scene gameplay against existing C# smoke tests and `dotnet run --project src/LittleEmpires.Simulator -- strategic 8` before adding more complexity.

## Important versioning constraints

- The canonical core is compiled for **.NET 8** and also has a separate **.NET Standard 2.1/C# 9 compatibility target** for Unity. We copy its source into Unity (not its .NET 8 DLL), keeping gameplay rules identical. GitHub Actions verifies both targets; Unity Editor compilation and IL2CPP are not yet tested.
- Existing Three.js browser save files are a different schema; **do not overwrite or silently migrate them**.
- `BuildingIndex` is stable only while buildings aren't demolished. Before adding demolition/real persistence, introduce stable building instance IDs.
- Runtime modifiers are currently fixed catalogue data in code; a future Unity editor could expose ScriptableObjects for presentation settings, but game balance should remain testable without Unity.
- The UI may animate construction progress, but it must never advance building construction or food inventory without calling C# simulation commands.

## What remains to be implemented in Unity

This handoff is **not a fully tested Unity integration**. A procedural scene generator, camera, basic building/road controls, tax and Marketplace HUD, and source sync are now in the starter. Still to verify the Unity Editor runtime, replace procedural placeholders with medieval prefabs, add proper input/UI, local persistence, Windows builds and QA. Track in [LE-026](https://github.com/MojasSimunovic/little-empires/issues/35).
