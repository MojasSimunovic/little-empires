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
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int islandSeed = 42;
        [SerializeField] private bool largeIsland;
        [SerializeField] private float cellSize = 6f;
        [SerializeField] private BuildingPrefabRegistry buildingPrefabs;
        [SerializeField] private GameObject woodenBridgePrefab;
        [SerializeField] private GameObject stoneBridgePrefab;

        private WorldGameSession _session;
        private IslandView _view;
        private Vector2 _menuScroll;
        private Vector2 _inspectorScroll;
        private bool _showInspector;
        private GUIStyle _statusStyle;
        private GUIStyle _headerStyle;
        private GridPosition? _selected;
        private int _selectedBuilding = -1;
        private string _message = "Select a building, then click a valid island tile.";
        private TurnSummary _lastTurn;

        private enum Tool
        {
            Inspect,
            Cottage,
            Farm,
            Fishery,
            LumberCamp,
            Quarry,
            LumberMill,
            FoodProcessor,
            Marketplace,
            DirtRoad,
            StoneRoad,
            UpgradeHousing
        }

        private Tool _tool = Tool.Cottage;
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
                " stone | Tab: switch corner | " + (_roadPreview.Succeeded ? "Release to build" : _roadPreview.Failure.ToString());
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
                _message = result.Succeeded ? "Road route built: " + result.Segments.Count + " plots." : "Route rejected: " + result.Failure;
                if (result.Succeeded) _view.UpdateRoads(_session.Snapshot());
            }
            CancelRoad();
        }

        private void Start()
        {
            _session = WorldGameSession.NewStrategicIsland(islandSeed, largeIsland ? 64 : 13, largeIsland ? 64 : 13);
            var snapshot = _session.Snapshot();
            var visualRoot = new GameObject("World visuals");
            _view = visualRoot.AddComponent<IslandView>();
            _view.BuildingPrefabs = buildingPrefabs;
            _view.WoodenBridgePrefab = woodenBridgePrefab;
            _view.StoneBridgePrefab = stoneBridgePrefab;
            if (largeIsland) {
                _view.LargeTerrain = visualRoot.AddComponent<LargeIslandTerrain>();
                _view.LargeTerrain.CellSize = cellSize;
                _view.LargeTerrain.Build(snapshot);
            }
            _view.Rebuild(snapshot);

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

            var sun = FindObjectsByType<Light>(FindObjectsSortMode.None)
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
        }

        private void Update()
        {
            if (_session == null) return;

            if (Input.GetKeyDown(KeyCode.R)) _tool = Tool.DirtRoad;
            if (Input.GetKeyDown(KeyCode.T)) _tool = Tool.StoneRoad;
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) EndSeason();
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelRoad(); _tool = Tool.Inspect; }
            if (_roadStart.HasValue && (_tool != _roadTool || Input.GetMouseButtonDown(1))) { CancelRoad(); return; }
            if (_roadStart.HasValue && Input.GetKeyDown(KeyCode.Tab)) ToggleRoadCorner();
            var roadTool = _tool == Tool.DirtRoad || _tool == Tool.StoneRoad;
            GridPosition picked = default;
            var hasPlot = !MouseInsideHud() && TryPick(out picked);
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

        internal const float HeaderHeight = 72f;
        internal static float DockWidth => Mathf.Clamp(Screen.width * .26f, 200f, 244f);

        internal static bool MouseInsideHud()
        {
            var point = Input.mousePosition;
            return point.x < DockWidth || point.y > Screen.height - HeaderHeight;
        }

        private void ClickTile(GridPosition position)
        {
            _selected = position;
            _view.Highlight(position);
            var snapshot = _session.Snapshot();
            _selectedBuilding = -1;

            foreach (var building in snapshot.Buildings)
            {
                if (building.Position.HasValue && building.Position.Value == position)
                {
                    _selectedBuilding = building.Index;
                    break;
                }
            }

            switch (_tool)
            {
                case Tool.Inspect:
                    _message = _selectedBuilding >= 0
                        ? snapshot.Buildings[_selectedBuilding].Name + " selected."
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

                case Tool.UpgradeHousing:
                    var upgraded = _selectedBuilding >= 0 &&
                        _session.UpgradeBuilding(_selectedBuilding);
                    _message = upgraded
                        ? "Cottage upgrade started."
                        : "Select a completed Cottage with affordable upgrade costs.";
                    if (upgraded) Refresh();
                    return;
            }

            var buildingId = BuildingIdForTool(_tool);
            if (buildingId == null) return;

            var preview = _session.PreviewBuilding(position, buildingId);
            if (!preview.Allowed)
            {
                _message = "Cannot build here: " + preview.Failure;
                return;
            }

            var success = _session.PlaceBuilding(position, buildingId);
            _message = success
                ? BuildingCatalog.Get(buildingId).Name + " construction started."
                : "Insufficient resources for construction.";
            if (success) Refresh();
        }

        private static string BuildingIdForTool(Tool tool)
        {
            switch (tool)
            {
                case Tool.Cottage: return BuildingCatalog.CottageId;
                case Tool.Farm: return BuildingCatalog.FarmId;
                case Tool.Fishery: return BuildingCatalog.FisheryId;
                case Tool.LumberCamp: return BuildingCatalog.LumberCampId;
                case Tool.Quarry: return BuildingCatalog.QuarryId;
                case Tool.LumberMill: return BuildingCatalog.LumberMillId;
                case Tool.FoodProcessor: return BuildingCatalog.FoodProcessorId;
                case Tool.Marketplace: return BuildingCatalog.MarketplaceId;
                default: return null;
            }
        }

        private void Refresh()
        {
            _view.Rebuild(_session.Snapshot());
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

        private void OnGUI()
        {
            if (_session == null) return;

            var snap = _session.Snapshot();
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
                _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            }

            var tint = GUI.color;
            GUI.color = new Color(.13f, .19f, .17f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, HeaderHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, HeaderHeight, DockWidth, Screen.height - HeaderHeight),
                Texture2D.whiteTexture);
            GUI.color = tint;

            GUILayout.BeginArea(new Rect(10, 5, Screen.width - 20, HeaderHeight - 10));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Year " + snap.Year + "  /  " + snap.Season, _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("End Season [Enter]", GUILayout.Height(25)))
                EndSeason();
            GUILayout.EndHorizontal();
            GUILayout.Label("Wood " + ResourceAmount(snap, Resource.Wood) +
                "    Stone " + ResourceAmount(snap, Resource.Stone) +
                "    Food " + ResourceAmount(snap, Resource.Food) +
                "    Coins " + ResourceAmount(snap, Resource.Coins) +
                "    |    Residents " + snap.Population + "/" + snap.HousingCapacity +
                "    Happiness " + snap.Happiness, _headerStyle);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(8, HeaderHeight + 8, DockWidth - 16,
                Mathf.Max(0, Screen.height - HeaderHeight - 16)));
            var showInspector = GUILayout.Toolbar(_showInspector ? 1 : 0,
                new[] { "Build", "Settlement" }) == 1;
            if (showInspector != _showInspector)
            {
                _showInspector = showInspector;
                GUIUtility.ExitGUI();
            }
            GUILayout.Label(_message, _statusStyle, GUILayout.ExpandWidth(true));
            if (_tool == Tool.DirtRoad || _tool == Tool.StoneRoad)
            {
                var clickMode = GUILayout.Toggle(clickRoadEndpoints, "Click start / click end");
                if (clickMode != clickRoadEndpoints) { CancelRoad(); clickRoadEndpoints = clickMode; }
            }

            if (_showInspector)
            {
                _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
                DrawInspector(snap);
                GUILayout.EndScrollView();
            }
            else
            {
                _menuScroll = GUILayout.BeginScrollView(_menuScroll);
                DrawBuildTools();
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }

        private void DrawBuildTools()
        {
            GUILayout.Label("BUILDINGS");
            ToolButton(Tool.Cottage, "Cottage");
            ToolButton(Tool.Farm, "Farm");
            ToolButton(Tool.Fishery, "Fishery");
            ToolButton(Tool.LumberCamp, "Lumber Camp");
            ToolButton(Tool.Quarry, "Quarry");
            ToolButton(Tool.LumberMill, "Lumber Mill");
            ToolButton(Tool.FoodProcessor, "Food Processor");
            ToolButton(Tool.Marketplace, "Marketplace");

            GUILayout.Space(6);
            GUILayout.Label("INFRASTRUCTURE");
            ToolButton(Tool.DirtRoad, "Dirt road (free) [R]");
            ToolButton(Tool.StoneRoad, "Stone upgrade [T] (2 stone)");
            ToolButton(Tool.UpgradeHousing, "Cottage > Townhouse");
            ToolButton(Tool.Inspect, "Inspect / select [Esc]");

            GUILayout.Space(8);
            GUILayout.Label("Right drag: orbit");
            GUILayout.Label("Scroll: zoom");
        }

        private void DrawInspector(WorldSnapshot snap)
        {
            GUILayout.Label("TAX POLICY");
            GUILayout.BeginHorizontal();
            TaxButton(TaxPolicy.Low, "Low", snap.TaxPolicy);
            TaxButton(TaxPolicy.Normal, "Normal", snap.TaxPolicy);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            TaxButton(TaxPolicy.High, "High", snap.TaxPolicy);
            TaxButton(TaxPolicy.VeryHigh, "Very high", snap.TaxPolicy);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (_selected.HasValue)
                GUILayout.Label("Plot: (" + _selected.Value.X + ", " +
                    _selected.Value.Z + ")");

            if (_selectedBuilding >= 0 && _selectedBuilding < snap.Buildings.Count)
            {
                var building = snap.Buildings[_selectedBuilding];
                GUILayout.Label(building.Name +
                    (building.IsComplete ? " — Complete" : " — Building"));
                GUILayout.Label("Workers needed: " + building.WorkersRequired);
                GUILayout.Label("Priority: " + building.WorkPriority);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Priority -"))
                    ChangePriority(building.Index, building.WorkPriority - 1);
                if (GUILayout.Button("Priority +"))
                    ChangePriority(building.Index, building.WorkPriority + 1);
                GUILayout.EndHorizontal();

                if (building.Id == BuildingCatalog.MarketplaceId && building.IsComplete)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("MARKETPLACE");
                    TradeButtons(Resource.Wood, "Wood");
                    TradeButtons(Resource.Stone, "Stone");
                    TradeButtons(Resource.Food, "Food");
                }
            }

            if (_lastTurn != null)
            {
                GUILayout.Space(9);
                GUILayout.Label("LAST SEASON");
                GUILayout.Label("Taxes +" + _lastTurn.TaxIncome +
                    "  Upkeep -" + _lastTurn.OperatingCosts);
                GUILayout.Label("Workers " + _lastTurn.WorkingResidents +
                    " / " + _lastTurn.StartingPopulation);
                GUILayout.Label("Food shortage " + _lastTurn.FoodShortage);
            }

        }

        private int ResourceAmount(WorldSnapshot snap, Resource resource)
        {
            return snap.Resources.TryGetValue(resource, out var count) ? count : 0;
        }

        private void ToolButton(Tool option, string title)
        {
            var old = GUI.backgroundColor;
            if (_tool == option) GUI.backgroundColor = new Color(.74f, .91f, .7f);
            if (GUILayout.Button(title, GUILayout.Height(25))) _tool = option;
            GUI.backgroundColor = old;
        }

        private void TaxButton(TaxPolicy policy, string title, TaxPolicy current)
        {
            var old = GUI.backgroundColor;
            if (policy == current) GUI.backgroundColor = new Color(.74f, .91f, .7f);
            if (GUILayout.Button(title))
            {
                _session.ChangeTaxes(policy);
                _message = "Taxation set to " + title + ".";
            }
            GUI.backgroundColor = old;
        }

        private void ChangePriority(int index, int priority)
        {
            if (_session.ChangeWorkPriority(index, priority))
                _message = "Workforce priority changed to " + priority + ".";
            else
                _message = "Priority must be from -10 to 10.";
        }

        private void TradeButtons(Resource resource, string title)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(58));
            if (GUILayout.Button("Buy 5"))
                Trade(TradeDirection.Buy, resource);
            if (GUILayout.Button("Sell 5"))
                Trade(TradeDirection.Sell, resource);
            GUILayout.EndHorizontal();
        }

        private void Trade(TradeDirection direction, Resource resource)
        {
            var outcome = _session.Trade(_selectedBuilding, direction, resource, 5);
            _message = outcome.Succeeded
                ? direction + " 5 " + resource + " for " + outcome.TotalCoins + " coins."
                : "Trading failed: " + outcome.Failure;
        }
    }
}
