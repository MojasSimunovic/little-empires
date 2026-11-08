using System;
using System.Linq;
using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    /// <summary>
    /// First Unity gameplay slice. All commands go to the authoritative
    /// C# WorldGameSession; this MonoBehaviour only handles input and UI.
    /// </summary>
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int islandSeed = 42;
        [SerializeField] private bool largeIsland;
        [SerializeField] private float cellSize = 6f;
        [SerializeField] private BuildingPrefabRegistry buildingPrefabs;
        [SerializeField] private NaturePrefabCatalog naturePrefabs;
        [SerializeField] private GameObject woodenBridgePrefab;
        [SerializeField] private GameObject stoneBridgePrefab;

        private WorldGameSession _session;
        private WorldSnapshot _cachedSnapshot;
        private IslandView _view;
        private bool _showInspector;
        private GridPosition? _selected;
        private int _selectedBuilding = -1;
        private string _message = "Select a building, then click a valid island tile.";
        private TurnSummary _lastTurn;

        private enum Tool
        {
            Inspect,
            VillageHall,
            Cottage,
            Farm,
            Fishery,
            LumberCamp,
            Quarry,
            LumberMill,
            FoodProcessor,
            Stoneworks,
            TradingHarbor,
            Marketplace,
            DirtRoad,
            StoneRoad
        }

        // On a fresh island, founding the administrative centre is the suggested first action.
        private Tool _tool = Tool.VillageHall;
        private GridPosition? _roadStart;
        private GridPosition? _roadEnd;
        private bool _xFirst = true;
        private Tool _roadTool;
        [SerializeField] private bool clickRoadEndpoints;
        private RoadPathPreview _roadPreview;

        private void CancelRoad()
        {
            _roadStart = null;
            _roadEnd = null;
            _roadPreview = null;
            if (_view != null) _view.ShowRoadPreview(null, null);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelRoad();
        }

        // Shared by pointer input and editor acceptance tests; never mutates the core until release.
        public void BeginRoad(GridPosition start, bool stone)
        {
            CancelRoad();
            _tool = stone ? Tool.StoneRoad : Tool.DirtRoad;
            _roadTool = _tool;
            _roadStart = start;
            PreviewRoad(start);
        }

        public void PreviewRoad(GridPosition end)
        {
            if (!_roadStart.HasValue) return;
            if (_roadEnd.HasValue && _roadEnd.Value == end && _roadPreview != null) return;
            _roadEnd = end;
            _roadPreview = _session.PreviewRoadPath(RoadPaths.Between(_roadStart.Value, end, _xFirst), _roadTool == Tool.StoneRoad);
            _view.ShowRoadPreview(_roadPreview, _session.Island.Roads, _roadTool == Tool.StoneRoad);
            _message = _roadPreview.Segments.Count + " plots | " + _roadPreview.CostWood + " wood / " + _roadPreview.CostStone +
                " stone | Tab: switch corner | " + (_roadPreview.Succeeded ? "Release to build" :
                    _roadPreview.Failure == RoadFailure.InvalidCrossing ? "Bridge only at marked crossings" :
                    _roadPreview.Failure.ToString());
        }

        public void ToggleRoadCorner()
        {
            _xFirst = !_xFirst;
            if (!_roadEnd.HasValue) return;
            var end = _roadEnd.Value;
            _roadEnd = null;
            PreviewRoad(end);
        }

        public void FinishRoad(bool commit)
        {
            if (commit && _roadPreview != null)
            {
                var result = _session.BuildRoadPath(_roadPreview.Segments, _roadTool == Tool.StoneRoad);
                _message = result.Succeeded ? "Road route built: " + result.Segments.Count + " plots." : "Route rejected: " + (result.Failure == RoadFailure.InvalidCrossing
                    ? "Bridges are only allowed at the marked crossings." : result.Failure.ToString());
                if (result.Succeeded)
                {
                    _cachedSnapshot = _session.Snapshot();
                    _view.UpdateRoads(_cachedSnapshot);
                }
            }
            CancelRoad();
        }

        private void Start()
        {
            _session = authoredTerrain != null
                ? WorldGameSession.NewStrategicIsland(CreateAuthoredMap())
                : WorldGameSession.NewStrategicIsland(islandSeed, largeIsland ? 64 : 13, largeIsland ? 64 : 13);
            var snapshot = _session.Snapshot();
            _cachedSnapshot = snapshot;
            var visualRoot = new GameObject("World visuals");
            _view = visualRoot.AddComponent<IslandView>();
            _view.BuildingPrefabs = buildingPrefabs;
            _view.NaturePrefabs = naturePrefabs;
            _view.WoodenBridgePrefab = woodenBridgePrefab;
            _view.StoneBridgePrefab = stoneBridgePrefab;
            if (largeIsland) {
                _view.LargeTerrain = visualRoot.AddComponent<LargeIslandTerrain>();
                _view.LargeTerrain.CellSize = cellSize;
                if (authoredTerrain != null)
                    _view.LargeTerrain.UseAuthoredSurface(authoredTerrain, snapshot, authoredSeaLevel);
                else
                    _view.LargeTerrain.Build(snapshot);
            }
            _view.Rebuild(snapshot);
            if (authoredTerrain != null)
                _view.UseAuthoredDecorations(authoredDecorations);
            else
                _view.InitializeNature(snapshot);
            _view.ConfigureCrossingMarkers(_session.Island.RiverCrossings);

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200f;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.26f, .48f, .53f);
            var controller = camera.GetComponent<IslandCamera>();
            if (controller == null) controller = camera.gameObject.AddComponent<IslandCamera>();
            controller.Frame(camera, _view.LandBounds);

            var sun = FindObjectsByType<Light>()
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (sun == null)
            {
                sun = new GameObject("Warm afternoon sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(1f, .88f, .71f);
            sun.intensity = 1.18f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .64f;
            sun.transform.rotation = Quaternion.Euler(39f, -37f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .70f, .73f);
            RenderSettings.ambientEquatorColor = new Color(.51f, .55f, .48f);
            RenderSettings.ambientGroundColor = new Color(.32f, .31f, .25f);
            _message = "Choose a tool, then click a plot.";
            _activeHud = _hud = new IslandHud(this);
        }

        private void LateUpdate() => _hud?.Sync();

        private void Update()
        {
            if (_session == null) return;
            if (_hud != null && _hud.HasModal)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _hud.CloseDemolition();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) SelectTool(Tool.DirtRoad);
            if (Input.GetKeyDown(KeyCode.T)) SelectTool(Tool.StoneRoad);
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) EndSeason();
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelRoad(); _tool = Tool.Inspect; _showInspector = false; }
            if (_roadStart.HasValue && (_tool != _roadTool || Input.GetMouseButtonDown(1))) { CancelRoad(); return; }
            if (_roadStart.HasValue && Input.GetKeyDown(KeyCode.Tab)) ToggleRoadCorner();
            var roadTool = _tool == Tool.DirtRoad || _tool == Tool.StoneRoad;
            _view.SetCrossingMarkersHighlighted(roadTool);
            GridPosition picked = default;
            var hasPlot = !MouseInsideHud() && TryPick(out picked);
            var buildingId = BuildingIdForTool(_tool);
            var definition = buildingId == null ? null : BuildingCatalog.Get(buildingId);
            if (hasPlot && definition != null)
                _view.ShowFootprintPreview(picked, definition.Width, definition.Height,
                    _session.PreviewBuilding(picked, buildingId).Allowed && _session.State.CanAfford(definition.Cost));
            else
                _view.ShowFootprintPreview(null);

            // Definite assignment when the pointer is over UI.
            if (!hasPlot) picked = default;
            if (roadTool)
            {
                if (Input.GetMouseButtonDown(0) && hasPlot)
                {
                    if (clickRoadEndpoints && _roadStart.HasValue) { PreviewRoad(picked); FinishRoad(true); }
                    else BeginRoad(picked, _tool == Tool.StoneRoad);
                }
                if (_roadStart.HasValue && hasPlot) PreviewRoad(picked);
                if (!clickRoadEndpoints && Input.GetMouseButtonUp(0)) FinishRoad(hasPlot);
                return;
            }
            if (Input.GetMouseButtonDown(0) && hasPlot) ClickTile(picked);
        }

        private bool TryPick(out GridPosition picked)
        {
            picked = default;
            var camera = Camera.main;
            if (camera == null) return false;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, camera.farClipPlane);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (_view.LargeTerrain != null && _view.LargeTerrain.Pick(hit, out picked)) return true;
                var tile = hit.collider.GetComponent<IslandTileTarget>();
                if (tile == null) continue;
                picked = tile.Position;
                return true;
            }
            return false;
        }

        private void ClickTile(GridPosition position)
        {
            _selected = position;
            _view.Highlight(position);
            var snapshot = _session.Snapshot();
            _selectedBuilding = -1;
            BuildingView selected = null;

            foreach (var building in snapshot.Buildings)
            {
                if (building.OccupiedPlots.Contains(position))
                {
                    selected = building;
                    _selectedBuilding = building.InstanceId;
                    _view.Highlight(building.Position, building.Width, building.Height);
                    break;
                }
            }

            _showInspector = _selectedBuilding >= 0 && _tool != Tool.DirtRoad && _tool != Tool.StoneRoad;

            // Existing Hall can always be opened by clicking its plot, even with
            // a build tool selected. Do not duplicate it or route input to its prefab.
            if (_selectedBuilding >= 0 &&
                IsCivicHall(selected.Id) &&
                _tool != Tool.DirtRoad && _tool != Tool.StoneRoad)
            {
                _tool = Tool.Inspect;
                _showInspector = true;
                _message = selected.Name + " selected.";
                return;
            }

            switch (_tool)
            {
                case Tool.Inspect:
                    _message = _selectedBuilding >= 0
                        ? selected.Name + " selected."
                        : "Plot (" + position.X + "," + position.Z + ") selected.";
                    return;

                case Tool.DirtRoad:
                    var dirt = _session.PlaceDirtRoad(position);
                    _message = dirt.Succeeded
                        ? "Free dirt road built."
                        : "Road rejected: " + dirt.Failure;
                    if (dirt.Succeeded) Refresh();
                    return;

                case Tool.StoneRoad:
                    var stone = _session.UpgradeStoneRoad(position);
                    _message = stone.Succeeded
                        ? "Dirt road upgraded for 2 stone."
                        : "Upgrade rejected: " + stone.Failure;
                    if (stone.Succeeded) Refresh();
                    return;
            }

            var buildingId = BuildingIdForTool(_tool);
            if (buildingId == null) return;

            var preview = _session.PreviewBuilding(position, buildingId);
            if (!preview.Allowed)
            {
                _message = preview.Failure switch
                {
                    PlacementFailure.OutsideMap => "The full building footprint must fit on the island.",
                    PlacementFailure.Water => "Part of the footprint is underwater.",
                    PlacementFailure.AlreadyOccupied => "Part of the footprint is occupied by a building.",
                    PlacementFailure.RoadPresent => "Part of the footprint crosses a road.",
                    PlacementFailure.RequiresLevelSite =>
                        "This plot is too steep for a building. Choose a level shelf or terrace.",
                    PlacementFailure.RequiresSettlementTier =>
                        "Requires a completed Town Hall to unlock this building.",
                    PlacementFailure.CivicHallAlreadyBuilt =>
                        "Only one Village Hall or Town Hall is allowed.",
                    _ => "Cannot build here: " + preview.Failure
                };
                return;
            }

            var success = _session.PlaceBuilding(position, buildingId);
            _message = success
                ? BuildingCatalog.Get(buildingId).Name + " construction started."
                : "Insufficient resources for construction.";
            if (success)
            {
                if (buildingId == BuildingCatalog.VillageHallId)
                {
                    _selectedBuilding = _session.Settlement.Buildings.Last().InstanceId;
                    _showInspector = true;
                    _tool = Tool.Inspect;
                }
                Refresh();
            }
        }

        private static string BuildingIdForTool(Tool tool)
        {
            switch (tool)
            {
                case Tool.VillageHall: return BuildingCatalog.VillageHallId;
                case Tool.Cottage: return BuildingCatalog.CottageId;
                case Tool.Farm: return BuildingCatalog.FarmId;
                case Tool.Fishery: return BuildingCatalog.FisheryId;
                case Tool.LumberCamp: return BuildingCatalog.LumberCampId;
                case Tool.Quarry: return BuildingCatalog.QuarryId;
                case Tool.LumberMill: return BuildingCatalog.LumberMillId;
                case Tool.FoodProcessor: return BuildingCatalog.FoodProcessorId;
                case Tool.Stoneworks: return BuildingCatalog.StoneworksId;
                case Tool.TradingHarbor: return BuildingCatalog.TradingHarborId;
                case Tool.Marketplace: return BuildingCatalog.MarketplaceId;
                default: return null;
            }
        }

        private void Refresh()
        {
            _cachedSnapshot = _session.Snapshot();
            _view.Rebuild(_cachedSnapshot);
            if (_selected.HasValue && _session.Island.TryGetBuilding(_selected.Value, out var building))
                _view.Highlight(building.Position, building.Definition.Width, building.Definition.Height);
            else
                _view.Highlight(_selected);
        }

        private void EndSeason()
        {
            CancelRoad();
            _lastTurn = _session.EndSeason();
            _message = "Season resolved. +" + _lastTurn.NewResidents +
                " residents, taxes +" + _lastTurn.TaxIncome +
                ", upkeep -" + _lastTurn.OperatingCosts +
                ", happiness " + _lastTurn.HappinessChange.ToString("+0;-0;0");
            Refresh();
        }

        private static bool IsCivicHall(string id) =>
            id == BuildingCatalog.VillageHallId || id == BuildingCatalog.TownHallId;

        private void ChangePriority(int index, int priority)
        {
            if (_session.ChangeWorkPriority(index, priority))
            {
                _message = "Workforce priority changed to " + priority + ".";
                _cachedSnapshot = _session.Snapshot();
            }
            else
                _message = "Priority must be from -10 to 10.";
        }

        private void Trade(int instanceId, TradeDirection direction, Resource resource, int units)
        {
            var outcome = _session.Trade(instanceId, direction, resource, units);
            _message = outcome.Succeeded
                ? direction + " " + units + " " + resource + " for " + outcome.TotalCoins + " coins."
                : "Trading failed: " + outcome.Failure;
            if (outcome.Succeeded)
                _cachedSnapshot = _session.Snapshot();
        }
    }
}
