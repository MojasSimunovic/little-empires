# Little Empires — First Unity 6 Playable Island (Windows)

**Goal:** Run the real `LittleEmpires.Core` simulation in Unity 6, using a seeded 3D island and placeholder cottage/industry meshes. Once it works, replace the procedural placeholder art with the Medieval Village MegaKit.

## 1. Install and create a URP project

1. Install Unity Hub from https://unity.com/download.
2. Install **Unity 6.3 LTS** in Hub; include **Windows Build Support (IL2CPP)** if offered.
3. In Hub > Projects > New project, choose **Universal 3D** (URP), not HDRP or Built-in 3D.
4. Name it `LittleEmpires` and choose a location such as `C:\Dev\LittleEmpires`.
5. After the editor opens, in **Edit > Project Settings > Player > Other Settings**, choose **Active Input Handling = Both** if the new template has disabled legacy `UnityEngine.Input`. Unity may ask to restart.

**Why:** Our `IslandCamera` and tile-click prototype use Unity's legacy Input API for simplicity. We can switch to Unity Input System after the first slice works.

## 2. Get the repository on Windows

The repo is **private**, so clone it while signed in to your GitHub account. Use GitHub Desktop (File > Clone Repository) or Git CLI:

```powershell
cd C:\Dev
git clone https://github.com/MojasSimunovic/little-empires.git
cd little-empires
```

If you use GitHub Desktop, navigate in PowerShell to the local clone folder instead.

## 3. Install the Unity starter

Close the Play mode if Unity is running, and from the repository root run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-UnityStarter.ps1 -UnityProject "C:\Dev\LittleEmpires"
```

Replace the path with **the actual location of the Unity project containing `Assets` and `Packages`**. This script copies:
- All `src/LittleEmpires.Core/*.cs` authoritative simulation files, plus the conditional `IsExternalInit` marker, into `Assets/LittleEmpires/Core`.
- Two assembly definition files to isolate the Unity-independent `LittleEmpires.Core` and Unity view scripts.
- Runtime `GameBootstrap`, `IslandView`, `IslandCamera`, plus the editor's `CreateStarterScene` menu script.

No third-party assets are required. The original gameplay source remains the single source of truth.

### After core gameplay changes

Whenever new commits modify `src/LittleEmpires.Core`, pull the latest repository and re-run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-UnityStarter.ps1 -UnityProject "C:\Dev\LittleEmpires" -CoreOnly
```

## 4. Create the scene and press Play

1. Return to Unity; let it compile the copied scripts. Open **Window > General > Console** and ensure there are **no red compilation errors**.
2. From the Unity top toolbar choose **Little Empires > Create Starter Island Scene**.
3. This creates and saves `Assets/LittleEmpires/Scenes/StarterIsland.unity` and adds it to the build scene list.
4. Press the **Play** button.
5. Use **Game** tab (not Scene tab) for mouse interaction.

**Controls:** Click terrain to place selected building; right mouse drag to rotate, wheel to zoom, **R** dirt road, **T** stone road upgrade, **E** End Season, **Esc** Inspect. Use the **Build** tab for buildings, roads and upgrades. Use **Settlement** for taxes, worker priority, trade and the last season summary. Select a building with the Inspect tool to adjust its priority or trade at a completed Marketplace with a road connection to completed housing. The top bar keeps resources and End Season visible while either tab scrolls.

The 3D world comes from `WorldGameSession.NewStrategicIsland(seed: 42)`, and `WorldSnapshot` tells Unity what to draw. The building/road costs, terrain validation, tax rates, trade rules, production and workforce decisions **all run inside our real C# simulation**.

## 5. What you'll see

This is a **first vertical slice**, not finished Tiny Glade art. It renders:
- A seeded 13×13 island with water, grassy plots, fertile land, forest trees and rocks/mountains.
- Simple cream-colored cottages with red roofs, placeholder Farms, Quarries, Fisheries, Mills and Marketplaces.
- Free dirt road tiles and stone road upgrades.
- A seasonal HUD displaying resources, happiness, taxation and population.
- Construction progress and basic buildings, all driven from the C# world model.

**Art goal:** Once the simulation and input are stable, swap placeholder houses and vegetation for the Quaternius Medieval Village MegaKit, customize materials and add soft ambient details.

## 6. Troubleshooting

| Problem | Solution |
|---|---|
| `git` command not found | Install Git for Windows or clone using GitHub Desktop. |
| `Not a Unity project` from script | Pass the folder with `Assets` and `Packages/manifest.json`, not the repo folder. |
| Unity compiler can't resolve `LittleEmpires.Core` | Check that `Assets/LittleEmpires/Core` contains **all** `.cs` files and `LittleEmpires.Core.asmdef`; let Unity finish recompiling. |
| `The type IsExternalInit is not defined` | Re-run the installer, which copies `src/LittleEmpires.Core/IsExternalInit.cs`. |
| `Input.GetMouseButtonDown` error at runtime | Set Active Input Handling to **Both** and restart Unity. |
| No toolbar menu | Resolve any C# compile errors first, then check `Assets/LittleEmpires/Editor/CreateStarterScene.cs`. |
| Scene is grey or pink | Start from Universal 3D template; check URP is installed and set as active pipeline. |
| Nothing happens when clicking tiles | Make sure Game view is active and click a plot in the island viewport below the header and to the right of the dock. |
| Some building placement fails | This is intentional terrain validation; choose eligible plots or view feedback text. |

## Architectural note

We refactored `src/LittleEmpires.Core` to use **C# 9-compatible syntax** without duplicating the game logic. A separate `LittleEmpires.Core.UnityCompatibility.csproj` targets **.NET Standard 2.1** and is now built in GitHub Actions; the existing net8.0 core and console smoke tests remain too.

We **cannot guarantee the Unity Editor compiles** until you run this on your Windows PC. The next task is to take any first-run errors from the Console and fix them, then connect better visual prefabs.

## Repository status

- UNITY-001: C# 9 conversion, .NET Standard compatibility target, copy installer, CI compatibility build — prepared, pending latest CI and real Unity Editor confirmation.
- UNITY-002: Bootstrapped procedural scene, camera and HUD (first slice).
- UNITY-003/004: Basic building/road/tax/trade commands implemented through the existing `WorldGameSession`; visuals and UI intentionally simple.
- UNITY-005: Proper saves, Tiny Glade-inspired art, animations, Windows player build and playtesting — future work.
