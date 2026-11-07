# Little Empires — next Unity milestone: drag-to-build roads and automatic bridges

**Status:** implementation brief, not yet implemented.  
**References:** [#43 — Drag-to-build roads](https://github.com/MojasSimunovic/little-empires/issues/43); [#44 — Automatic bridges](https://github.com/MojasSimunovic/little-empires/issues/44); [#41 — Flat city island, corner mountain and river](https://github.com/MojasSimunovic/little-empires/issues/41).

## Product decision

The user wants to build an entire city on the existing **64×64, mostly-flat** `LargeIsland`, with a small river, not undulating building plots. Roads should be intuitive: **click-and-drag** to construct a route in one action; optionally **click start / click end** using the same path logic. The tool **automatically builds a wooden bridge** where a dirt road crosses an authorized river; upgrading the same route to stone automatically converts it to a **stone bridge**. There is no separate bridge tool.

The terrain flattening and river geography are tracked by #41. Work in the **actual playable `LargeIsland` scene**, preserving `StarterIsland`, imported Quaternius assets, Meadow Cottage and authoritative C# simulation.

## Confirmed existing state (GitHub commit 87f41bbe)

- `unity/LittleEmpires/Assets/LittleEmpires/Runtime/GameBootstrap.cs`: `Update()` currently handles only `Input.GetMouseButtonDown(0)`; `ClickTile()` dispatches individual `_session.PlaceDirtRoad()` / `UpgradeStoneRoad()`, calling `Refresh()` after each action.
- `src/LittleEmpires.Core/IslandRoadNetwork.cs`: `EvaluateDirtPlacement` forbids water, forests, mountains, occupied buildings, existing roads. `EvaluateStoneUpgrade` validates an existing dirt road and `GameState` resources; `RoadSurface` is Dirt/Stone. `ConnectedComponent` is FOUR-directional.
- `src/LittleEmpires.Core/WorldGameSession.cs`: public boundary for road actions and `WorldSnapshot.Roads`; no multi-segment atomic transaction yet.
- `unity/LittleEmpires/Assets/LittleEmpires/Runtime/IslandView.cs`: `DrawRoad` currently creates a floating-ish square cube per road tile at `center.y + 0.22f`, and `Rebuild` recreates the entire dynamic scene graph on every command.
- `LargeIslandTerrain.Pick` maps TerrainCollider hit to `GridPosition`. `BuildingPrefabRegistry` now handles real Meadow Cottage prefabs.
- **There is currently no explicit river/bridge data model or bridge prefab in the committed project.** Do not assume the visual water plane or `TerrainKind.Water` is a river; those are currently ocean/coast. Do not allow bridges over every water cell.

## Existing models and external bridge models

Inventory of committed Quaternius FBX assets found **no ready-made bridge model** in either imported pack.

We have useful medieval kit pieces to **assemble** a handcrafted bridge: `Floor_WoodDark.fbx`, `Floor_WoodLight.fbx`, `Wall_Arch.fbx`, `Wall_UnevenBrick_Straight.fbx`, `Prop_WoodenFence_Single.fbx`, `Prop_WoodenFence_Extension1.fbx`, `Prop_WoodenFence_Extension2.fbx`, and rock path stones from the Stylized Nature MegaKit. These are **raw source pieces**, not proof that a complete bridge prefab exists.

Better starting geometry: **Kenney Nature Kit**, free CC0, https://kenney.nl/assets/nature-kit (not yet committed as a package). Public inventories show `bridge-wood`, `bridge-wood-round`, `bridge-center-wood`, `bridge-side-wood`, and matching `stone` and `stoneround` variants. These are relatively simple low-poly models, so **recolor/retexture/customize** with shared URP materials and Quaternius parapets/planks/rocks until both bridges look cohesive beside our cottage. Inspect the actual downloaded package before assuming file spelling/format.

**Recommendation:** Download/import Kenney Nature Kit once, pick the best wooden and stone versions, create two finished, scale-calibrated prefabs:
- `Assets/LittleEmpires/Art/Bridges/WoodenRiverBridge.prefab`
- `Assets/LittleEmpires/Art/Bridges/StoneRiverBridge.prefab`

Fit one bridge crossing to a **one-cell-wide river** with approximately **6-unit-wide bank approaches**, at a stable road deck height. The water surface should remain visible beneath. Include railings/stone arches without blocking selection raycasts. If Kenney appears stylistically too crude, assemble a more detailed version using Quaternius pieces instead; don't block the gameplay milestone on one art pack.

## Implementation plan

### 1. Make drag-to-build roads work on dry land FIRST (#43)

**UX**
- Select Dirt Road (R); pressing, dragging and releasing across the island previews/commits an entire connected, orthogonal path. Quick click on one plot still builds one road segment.
- Straight segments on aligned axes. Otherwise render an L with **horizontal-then-vertical** as default, **Tab** toggling vertical-then-horizontal. Path has all intermediate plots, no diagonal corner cutting.
- Ghost segments preview green when valid, red for invalid (with failure reason), gray/neutral when already a compatible road. Show segment count and cumulative cost.
- Escape cancels. Right-click cancels if a route is pending; retain right-drag camera orbit if not drawing, and do not commit when pointer is over HUD.
- Optional click-start/click-end mode as a later preference; don't let it conflict with one-click single-tile placement.
- Stone Road tool (T) uses identical drag preview; only dirt segments cost stone; already-stone portions are pass-through, not recharged.

**Core rules**
- Add a **pure route preview / validation API** and **atomic bulk placement APIs** to C# core (both dirt path and stone upgrade path). Validate all distinct coordinates, occupancy, terrain and **aggregate resources** before modifying any road or resource.
- Suggested exposed model: `RoadPathPreview` with `Segments`, `CostWood`, `CostStone`, `Failure`, `FailureAt`, `Succeeded`. Names are flexible; design it as core-owned validation data, not Unity-only rules.
- Over existing road segments, choose clear semantics: permit pass-through without duplicate placement for same-grade/existing upgraded segments. Reject invalid terrain/occupied tiles. All-or-nothing for invalid or unaffordable route.
- Continue to use four-neighbor connectivity. Keep the existing single-tile APIs working and do not change production/market rules.
- Add C# tests: line, corner, switching corners, blocked cell atomic rollback, existing road pass-through, exact stone cost, insufficient stone zero-change.

**Unity**
- Refactor `GameBootstrap.Update()` into an explicit road draw state: idle → drag start → preview update → release/commit/cancel.
- Use existing `LargeIslandTerrain.Pick` for pointer-to-grid. Keep a reusable ghost preview root; never call full `Rebuild` per mouse move.
- Build roads flat and flush with ground: avoid the present `center + 0.22f` elevated square-block appearance. Connect adjacent tiles without gaps or visible slab edges; avoid visual encroachment into placed cottages.
- Commit route once using `WorldGameSession`. During seasons, keep static Terrain and biome vegetation persistent and ideally update only changed road objects.
- Retain camera orbit/zoom/pan and allow building/selection tools to coexist.
- Test straight/L paths at close and far zoom, HUD edges, cancellation, green/red previews and C# atomics in Play mode.

### 2. Add a SMALL playable river and bridges (#44, depends on #41 river)

**Terrain and authoritative data**
- Add a deterministic narrow river waterway, ideally flowing from the corner mountain toward the coast with **designated one-cell crossings**. Keep main city basin mostly flat.
- Distinguish river cells from ocean cells using an explicit core river category or separate `WaterwayMask`/crossing metadata. Preserve current `TerrainKind.Water` rejection for ocean, shore and unsupported water.
- Bridge span rules: at least one supported crossing perpendicular to river flow, backed by valid opposite banks, no turns midbridge, no duplicate bridge and no buildings on water bridge cells. Road connectivity counts bridge segments like normal roads.
- Do not confuse physical river water width with logic: if a river occupies a narrow strip within a plot, document/visualize exactly how it maps to river-crossing plot(s) and the Terrain raycast.
- If adding navigable crossing logic requires more work, complete dry-land roads as a coherent shippable milestone before modifying the river.

**Bridge construction and cost**
- Dirt road + valid river crossing = WoodenRiverBridge automatically, not free. Suggested balancing defaults **8 wood per one-cell crossing**.
- Upgrade that route to stone = StoneRiverBridge automatically; suggested defaults **8 stone per one-cell crossing**, normal dirt→stone road remains **2 stone per ordinary tile**. All amounts configurable in authoritative core and displayed in preview.
- Validate full route and resource costs atomically before deduction; mixed land+river path should never be half-created.
- Expose bridge/road **visual type and grade** in C# snapshot so Unity instantiates the right prefab. A wood bridge is still logically part of the four-connected road network.
- Bridge prefab positioning: correct river water level, bank deck alignment, orientation from crossing axis, no floating/sunken bases. Water renders below the bridge; bridge colliders must not occlude Terrain picking unexpectedly.
- Tests: permitted river crossing, impossible open-sea crossing, sideways bridge, invalid landing, atomic insufficient wood/stone, bridge upgraded after seasons, connectivity across water.

### 3. Showcase and acceptance

In **LargeIsland Play mode**, demonstrate:
1. Dirt road drag of >10 plots through flat city land and L bend, both corner options; quick single click.
2. A red blocked route with ZERO partial road mutations.
3. A drag of 5 dirt-to-stone upgrades costing exactly 10 stone.
4. A legal river crossing that produces a **real wooden bridge model**, with water visible below.
5. Upgrading the crossing to a **real stone bridge model** without breaking road connectivity.
6. Cottages, roads, river and bridges remain aligned after 2+ seasons and scene updates.
7. Close-up and overhead Game view screenshots, Console 0 errors, meaningful play-mode profiling.
8. Keep git changes reviewable and sync canonical `src/LittleEmpires.Core` into `Assets/LittleEmpires/Core` using existing installer, rather than editing only the Unity duplicate.

**Definition of done:** Gameplay and visuals work in the actual island; a document or test scene alone is not enough. Do not claim bridges complete if there is no legal river crossing in C# or no bridge prefab rendered in the playable scene.

## Copy/paste Codex kickoff

> Implement `docs/ROAD_AND_BRIDGE_NEXT_STEP.md` in Little Empires through Unity MCP. Start with #43 dry-land transactional drag-to-build roads, then #44 one-cell river crossings and automatic wooden/stone bridges. Preserve the flat LargeIsland terrain and the real Meadow Cottage. Verify the models we already have; if Kenney Nature Kit hasn't been imported, assemble an art placeholder from Quaternius pieces and clearly report it as provisional, or ask me to import Kenney before final bridge art. Do not claim completion without actual Game view screenshots, C# tests and zero Unity Console errors.
