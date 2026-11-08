# Little Empires — Village Hall / Town Hall: Unity MCP implementation brief

**Status:** C# feature branch prepared; Unity art and in-editor verification are NOT yet complete.  
**Branch:** `feature/village-town-hall-2026-11-08`  
**Baseline:** `3416f9b` on `main`, with unfinished stabilisation work in draft PRs.  
**Target:** `unity/LittleEmpires`, Unity 6 URP, 64×64 authored island, 6-world-unit logical tiles.

## Product contract (keep it small)

- One **Village Hall** per settlement; it is player-placeable, suggested as the very first building (default tool).
- The **Town Hall** is its only upgrade: one upgrade-only target, not another building the player can place.
- Clicking either building opens **Settlement Administration**, not an economy running in a MonoBehaviour.
- Show population/housing, happiness, treasury/resources; 4 tax policies; projected next-season taxes; last-season **actual** tax income, operating upkeep, workforce and food. Do not present tax minus upkeep as total cash flow (trade and construction are excluded).
- Upgrade: at least **100 residents**, **60 wood, 25 stone, 300 coins**, paid once; **2 seasons**; original Hall remains functional while upgrading. These are **provisional balance values**.
- Village Hall placement: **35 wood, 15 stone, 30 coins**, one season construction. Neither hall generates population, goods, extra taxes or automatic buffs.
- Use existing `WorldGameSession` methods `PlaceBuilding`, `UpgradeBuilding`, `ChangeTaxes`, `Snapshot`, `EndSeason`. `src/LittleEmpires.Core` is authoritative; do NOT make separate economy/tax scripts.
- Reuse exactly the housing upgrade infrastructure: `BuildingUpgradeDefinition`, `Building.StartUpgrade/ProgressUpgrade`, `Settlement.TryStartUpgrade`. The C# branch already implements this.
- Keep **Cottage → Townhouse** fully functional, including existing local 5 visual variations and residence-detail work. Do not recreate or overwrite them.
- Do NOT add a new multi-tile placement system, policies, diplomatic UI, service radius, new tax modes, new people simulation or new scenes in this task.
- The current first-build recommendation is not a hard "no other buildings until Hall" rule. Do not add that gate without a separate balance decision.

## Phase 0 — Safe preparation

1. Inspect **local Git status** BEFORE switching/cherry-picking anything. Current local MCP/Unity work may contain uncommitted assets and more recent cottage variants than GitHub `main`.
2. Read `AGENTS.md`, `docs/STATUS.md`, this document and current PRs #46 and #49. Do not inadvertently overwrite scene changes from stabilisation PRs.
3. Integrate the feature branch into the local Unity workspace safely. Prefer a clean checkpoint and normal Git merge/rebase; no hard reset or force checkout. If merging conflicts with local work, resolve preserving new local authored art and active Unity scene.
4. Verify Unity Editor connection via MCP (read-only first): project `unity/LittleEmpires`, active scene, Console, object hierarchy, packages and prefabs.
5. Confirm the authoritative core copies are in sync (`src/LittleEmpires.Core` to `unity/LittleEmpires/Assets/LittleEmpires/Core`). The branch includes mirrored core files, but future changes must go through `tools/Install-UnityStarter.ps1 -CoreOnly`, not manual Unity mirror edits.

## Phase 1 — Two *related* but distinct civic models

**Art direction:** Match the existing cottages/townhouses shown in the November 8 screenshots: friendly European village / Tiny Glade mood, warm cream plaster, restrained dark timber trim, terracotta-red tiled gabled roofs, warm windows, small chimneys, readable stylized silhouette. Make the civic building identifiable from an elevated isometric camera without looking like an enormous castle.

### Actual size constraint — very important

`IslandView.DrawBuilding` currently renders one building centred on **one logical 6×6-world-unit plot**. Do NOT use the earlier suggested "2–3 cottage footprints"; that would overlap occupied plots and roads. Instead get greater *visual mass* from wider façades, extra height, bell/clock cupola, decorative porch, flags and a compact nearby public square composed of **other free tiles**. Keep the entire building (including roof/eaves/porch/tower) within roughly **5.8 m × 5.8 m** viewed from above.

**Village Hall (Tier 1):**
- Approximately 5.2–5.5 m front width, 4.4–4.9 m depth, around 4.5–5.8 m total height, scaled against a cottage in Unity.
- One primary storey, prominent centrally placed oak entrance facing **world -Z** (same as the cottage), shallow front porch / low steps, 2–4 windows, horizontal dark wooden trim, stone plinth.
- Terracotta pitched roof, modest bell gable or small timber lantern/cupola on top, small noticeboard, 1–2 restrained flags/planters.
- Warm, approachable and humble; civic identity should be readable at gameplay zoom.

**Town Hall (Tier 2):**
- Roughly 5.5–5.8 m front width and 4.8–5.2 m depth, about 7–8.5 m tall including cupola (adjust visually to fit the current townhouse).
- Clear **upgrade from the same building**: same front-door orientation, matching palette and base structure; add a second architectural level / raised central mass, taller clock or bell turret, stronger stone base, formal roof, shutters, banners.
- Clearly more prestigious without changing the tile occupation. No detached columns, canopy or stairs over the plot boundary.
- Existing building identity/position persists during upgrade; only the visual ID changes upon completion.

**Technical modeling requirements:**
- Use existing available modular licensed meshes or Unity mesh/primitives; make original assembled prefabs and keep small visual variety in details. Do not depend on additional paid assets or external Blender/plugin installation.
- Author **two separate real prefabs** (not runtime-generated cubes pretending to be completed art):
  - `Assets/LittleEmpires/Art/Civic/VillageHall.prefab`
  - `Assets/LittleEmpires/Art/Civic/TownHall.prefab`
- Suggested hierarchy root: `VillageHall` or `TownHall` → `Foundation`, `Walls`, `Roof`, `DoorsAndWindows`, `TowerOrBell`, `Decorations`.
- Root prefab transform: **local position (0,0,0), rotation (0,0,0), scale (1,1,1)**; pivot at ground-level middle of its plot; lowest geometry should contact terrain at local y=0. Main door faces **-Z**.
- Use URP-compatible materials, shared material assets (avoid unnecessary material instances), normal shadow casting. Verify no missing/pink shaders in Game View or player build.
- Ensure 6 m is treated as world size: `IslandView.DrawBuilding` spawns a prefab inside a root scaled to `LargeTerrain.CellSize` and applies inverse scale to the prefab child. Do not manually multiply the prefab dimensions by 6 again.
- Avoid world colliders on prefab children: the current `IslandView` disables their colliders; selection uses the corresponding grid tile. Make sure hover/click reliably selects the correct plot.
- Keep geometry lightweight: modest polygon counts and renderer count, no huge transparent planes. Chimney smoke and subtle window lighting are optional only if stable/performance-safe. Model must be recognizable without particles.
- Let Unity create and track asset `.meta` files. Commit source prefabs, materials and `.meta` files, not Unity Library/Temp/obj or unlicensed asset packs.
- Create a *private* visual test lineup: Village Hall near cottages and Town Hall near townhouses in a test scene or temporary isolated region; compare front orientation, color palette, height and readability before integrating.

## Phase 2 — Register prefabs with the real game

1. Inspect existing `BuildingPrefabRegistry` and the serialized registry used by the playable `LargeIsland` scene (historically the `Building Prefabs.asset` registry in `Scenes/EnvironmentArtTestAssets`; confirm references instead of assuming).
2. Register exact stable IDs, **one prefab each**:
   - `village-hall` → `VillageHall.prefab`
   - `town-hall` → `TownHall.prefab`
3. Make sure playable `GameBootstrap.buildingPrefabs` points to this registry. Do not remove or replace cottage/townhouse variants.
4. `IslandView.DrawBuilding` already selects by `building.Id`; when `Village Hall → Town Hall` finishes, `SyncBuildings` sees the ID changed and rebuilds that instance with the new prefab while retaining position and index.
5. While upgrading, it is acceptable for the old Hall prefab to remain until the upgrade completes. Optional tiny upgrade scaffolding can be considered later, but do not let it block this milestone.
6. New-game initial action: default Village Hall tool should be selected, let player choose a level, available site, place the Hall, advance one season to complete. Do not automatically spawn it at the island centre.

## Phase 3 — Settlement Administration UI

The feature branch's `GameBootstrap.cs` already has the initial **OnGUI** inspector. Verify or improve it rather than writing a second competing screen.

**Opening:** click the tile occupied by the Hall (in Inspect or other non-road tool) → switch to Settlement tab → show administration. Existing normal buildings still have their standard inspector.

**Must show:**
- Correct title according to tier and construction/upgrade state.
- Population / total housing / available housing, happiness.
- Coins (treasury), stockpiled wood, stone, food.
- Four **existing** tax policies: Low, Normal, High, Very High. Reuse `TaxPolicy` and `TaxationSystem.Effects`. These are policy **buttons**, not a new 0–25% tax slider.
- Predicted next-season tax income = `TaxationSystem.CalculateIncome(snapshot.Population, snapshot.TaxPolicy)`. Label this an estimate, since population can change.
- Last completed season **actual** tax income and **actual** operating upkeep from `TurnSummary`; employed/unemployed, food consumed/shortage.
- An operating balance showing last-season taxes minus upkeep **excluding** construction spending, trade and other changes; do not call it full "net income".
- Village Hall only: visible upgrade requirements, population threshold, one-click upgrade action. Grey out upgrade if unfinished, already upgrading, population below 100, or materials insufficient.
- Town Hall: keep the same finance UI, no further upgrade button.

**Interactions:** `ChangeTaxes` updates policy and UI immediately; financial collection happens at **End Season**. Tax buttons should be disabled before Hall construction completes. `UpgradeBuilding` spends cost once, upgrade completes after 2 End Season actions. Taxes should keep flowing during the upgrade.

**UI notes:** UI is currently compact OnGUI in left side dock; make it readable at 1920×1080 and 1280×720 without clipping or accidental clicks-through. If the panel needs additional room, improve scroll handling first; do not rebuild the whole interface in UI Toolkit as part of this feature.

## Phase 4 — Acceptance tests (do not claim completion without these)

1. Build/smoke tests: `dotnet build LittleEmpires.sln --configuration Release -warnaserror` if solution present, otherwise individual project builds; `dotnet run --project tests/LittleEmpires.SmokeTests`; run simulator. Check CI Actions for the feature branch.
2. **Play Mode:** spawn no Hall at first; Village Hall tool selected. Place on a valid one-tile plot, pay 35 wood/15 stone/30 coins, verify construction state and position.
3. Attempt a second Village Hall and direct Town Hall: both refused **without** consuming resources. Check the Hall remains on the original plot.
4. End Season once; Village Hall prefab appears. Click it: tax controls and administration report show up; normal building inspection and existing cottage upgrades still work.
5. Change from Normal to High; verify projected revenue changes immediately, actual tax collection/happiness impact happens on the next season, and no collection happens merely from clicking tax policy.
6. Low-population Village Hall cannot upgrade and does not spend resources.
7. In a controlled test settlement with 100+ residents and sufficient stockpile: upgrade costs **60 wood + 25 stone + 300 coins** one time. First season preserves Village Hall and tax collection; second season changes model and ID to Town Hall in the exact same position.
8. Inspect upgraded Town Hall: prior tax policy persists, numbers are correct, and upgrade button is absent. Town Hall cannot be duplicated.
9. Verify both prefabs in daylight, at gameplay zoom, and in rain/dark lighting if readily available; test -Z door orientation and footprint (no clipping/overlap with roads/adjacent houses).
10. Repeat with multiple nearby cottage/townhouse visual variants; verify no prefab references, transforms, materials, scene data, bridges or other ongoing visual work were changed unintentionally.
11. Console: no missing-reference, null, material/shader, compile or serialization errors. Capture Game View screenshots for both completed tiers and Hall inspector.
12. If Play Mode or standalone Windows build cannot be run, explicitly mark **unverified**, with reason. No visual completion claim based solely on compiling code.

## Expected handoff report

Report (a) asset/prefab paths, (b) actual new/changed scripts, (c) snapshots or screenshots from Unity, (d) Play Mode and C# test results, (e) Git branch/commit(s), (f) any unresolved conflicts with cottage variant work or stabilization PRs. Keep `main` unchanged until accepted.

**No extra features during this pass.** Our success condition is **two attractive, registered Hall prefabs, one upgrade path and a truthful functional administration panel**.
