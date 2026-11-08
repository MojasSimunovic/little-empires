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
            StoneRoad,
            UpgradeHousing
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
                    _view.LargeTerrain.UseAuthoredSurface(authoredTerrain);
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
            _view.SetCrossingMarkersHighlighted(roadTool);
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

            // Existing Hall can always be opened by clicking its plot, even with
            // a build tool selected. Do not duplicate it or route input to its prefab.
            if (_selectedBuilding >= 0 &&
                IsCivicHall(snapshot.Buildings[_selectedBuilding].Id) &&
                _tool != Tool.DirtRoad && _tool != Tool.StoneRoad)
            {
                _tool = Tool.Inspect;
                _showInspector = true;
                _message = snapshot.Buildings[_selectedBuilding].Name + " selected.";
                return;
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
                        : "Requires a completed Cottage, 20 residents and upgrade materials.";
                    if (upgraded) Refresh();
                    return;
            }

            var buildingId = BuildingIdForTool(_tool);
            if (buildingId == null) return;

            var preview = _session.PreviewBuilding(position, buildingId);
            if (!preview.Allowed)
            {
                _message = preview.Failure switch
                {
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
                    _selectedBuilding = _session.Settlement.Buildings.Count - 1;
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

            var snap = _cachedSnapshot;
            if (snap == null) return;
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
                "    Happiness " + snap.Happiness +
                "    Tier: " + snap.Tier, _headerStyle);
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
            var snap = _cachedSnapshot;
            var isTown = snap.Tier >= SettlementTier.Town;
            var hallExists = snap.Buildings.Any(building => IsCivicHall(building.Id));
            GUILayout.Label("SETTLEMENT: " + snap.Tier.ToString().ToUpperInvariant());
            GUILayout.Label("VILLAGE BUILDINGS");
            ToolButton(Tool.VillageHall, "Village Hall (unique)", !hallExists);
            ToolButton(Tool.Cottage, "Cottage");
            ToolButton(Tool.Farm, "Farm");
            ToolButton(Tool.Fishery, "Fishery");
            ToolButton(Tool.LumberCamp, "Lumber Camp");
            ToolButton(Tool.Quarry, "Quarry");
            ToolButton(Tool.Marketplace, "Marketplace");

            GUILayout.Space(6);
            GUILayout.Label("TOWN INDUSTRIES" + (isTown ? "" : " (LOCKED)"));
            if (!isTown) GUILayout.Label("Unlock: upgrade Village Hall at 50 residents.");
            ToolButton(Tool.LumberMill, "Sawmill", isTown);
            ToolButton(Tool.FoodProcessor, "Food Processing Workshop", isTown);
            ToolButton(Tool.Stoneworks, "Stoneworks", isTown);
            ToolButton(Tool.TradingHarbor, "Trading Harbor", isTown);

            GUILayout.Space(6);
            GUILayout.Label("INFRASTRUCTURE");
            ToolButton(Tool.DirtRoad, "Dirt road (free) [R]");
            ToolButton(Tool.StoneRoad, "Stone upgrade [T] (2 stone)");
            var housingUnlocked = snap.Population >= HousingUpgradeCatalog.CottageToTownhouse.MinimumPopulation;
            ToolButton(Tool.UpgradeHousing, "Cottage > Townhouse (20 residents)", housingUnlocked);
            ToolButton(Tool.Inspect, "Inspect / select [Esc]");
            GUILayout.Space(8);
            GUILayout.Label("Right drag: orbit");
            GUILayout.Label("Scroll: zoom");
        }

        private void DrawInspector(WorldSnapshot snap)
        {
            if (_selectedBuilding >= 0 && _selectedBuilding < snap.Buildings.Count &&
                IsCivicHall(snap.Buildings[_selectedBuilding].Id))
            {
                DrawCivicHallInspector(snap, snap.Buildings[_selectedBuilding]);
                return;
            }

            GUILayout.Label("Select the Village Hall or Town Hall to manage taxes.");
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

                if (building.Id == BuildingCatalog.CottageId)
                {
                    GUILayout.Space(6);
                    var upgrade = HousingUpgradeCatalog.CottageToTownhouse;
                    GUILayout.Label("COTTAGE > TOWNHOUSE");
                    GUILayout.Label("Requires " + upgrade.MinimumPopulation + " residents.");
                    GUILayout.Label("Cost: " + upgrade.Cost[Resource.Wood] + " wood, " +
                        upgrade.Cost[Resource.Stone] + " stone, " +
                        upgrade.Cost[Resource.Coins] + " coins.");
                    var originalGuiState = GUI.enabled;
                    var affordable = upgrade.Cost.All(entry =>
                        snap.Resources.TryGetValue(entry.Key, out var amount) && amount >= entry.Value);
                    GUI.enabled = originalGuiState && building.IsComplete &&
                        !building.IsUpgrading && snap.Population >= upgrade.MinimumPopulation && affordable;
                    if (GUILayout.Button("Upgrade to Townhouse"))
                    {
                        if (_session.UpgradeBuilding(building.Index))
                        {
                            _message = "Townhouse upgrade started.";
                            Refresh();
                        }
                    }
                    GUI.enabled = originalGuiState;
                    if (building.IsUpgrading) GUILayout.Label("Upgrade in progress.");
                    else if (snap.Population < upgrade.MinimumPopulation)
                        GUILayout.Label("Need " + (upgrade.MinimumPopulation - snap.Population) + " more residents.");
                    else if (!affordable) GUILayout.Label("Not enough upgrade resources.");
                }

                if ((building.Id == BuildingCatalog.MarketplaceId ||
                    building.Id == BuildingCatalog.TradingHarborId) && building.IsComplete)
                {
                    var harbor = building.Id == BuildingCatalog.TradingHarborId;
                    var capacity = harbor
                        ? MarketplaceTrading.MaxUnitsPerHarborPerSeason
                        : MarketplaceTrading.MaxUnitsPerMarketPerSeason;
                    GUILayout.Space(5);
                    GUILayout.Label(harbor ? "TRADING HARBOR" : "MARKETPLACE");
                    GUILayout.Label("Trade quota: " + building.UnitsTradedThisSeason + "/" + capacity +
                        " units this season");
                    var quantity = harbor ? 20 : 5;
                    TradeButtons(Resource.Wood, "Wood", quantity);
                    TradeButtons(Resource.Stone, "Stone", quantity);
                    TradeButtons(Resource.Food, "Food", quantity);
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

        private static bool IsCivicHall(string id) =>
            id == BuildingCatalog.VillageHallId || id == BuildingCatalog.TownHallId;

        /// <summary>
        /// Civic building inspection only. All tax rules, treasury changes and
        /// upgrade commands remain in the Unity-independent simulation core.
        /// </summary>
        private void DrawCivicHallInspector(WorldSnapshot snap, BuildingView hall)
        {
            GUILayout.Label(hall.Name.ToUpperInvariant(), _headerStyle);
            if (!hall.IsComplete)
                GUILayout.Label("Under construction: " + hall.SeasonsRemaining + " season(s) remaining.");
            else if (hall.IsUpgrading)
                GUILayout.Label("Upgrading: " + hall.UpgradeSeasonsRemaining + " season(s) remaining.");

            GUILayout.Space(6);
            GUILayout.Label("SETTLEMENT");
            GUILayout.Label("Settlement tier: " + snap.Tier);
            GUILayout.Label("Residents: " + snap.Population + " / " + snap.HousingCapacity);
            GUILayout.Label("Available homes: " + Math.Max(0, snap.HousingCapacity - snap.Population));
            GUILayout.Label("Happiness: " + snap.Happiness + " / 100");
            GUILayout.Label("Treasury: " + ResourceAmount(snap, Resource.Coins) + " coins");
            GUILayout.Label("Wood " + ResourceAmount(snap, Resource.Wood) +
                "  Stone " + ResourceAmount(snap, Resource.Stone) +
                "  Food " + ResourceAmount(snap, Resource.Food));

            GUILayout.Space(8);
            GUILayout.Label("TAX POLICY");
            var currentEffects = TaxationSystem.Effects(snap.TaxPolicy);
            GUILayout.Label("Current: " + snap.TaxPolicy +
                "  (happiness " + currentEffects.HappinessPerSeason.ToString("+0;-0;0") +
                " / season from taxes)");
            var originalGuiState = GUI.enabled;
            GUI.enabled = hall.IsComplete;
            GUILayout.BeginHorizontal();
            TaxButton(TaxPolicy.Low, "Low", snap.TaxPolicy);
            TaxButton(TaxPolicy.Normal, "Normal", snap.TaxPolicy);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            TaxButton(TaxPolicy.High, "High", snap.TaxPolicy);
            TaxButton(TaxPolicy.VeryHigh, "Very high", snap.TaxPolicy);
            GUILayout.EndHorizontal();
            GUI.enabled = originalGuiState;
            GUILayout.Label("Estimated next-season tax income: +" +
                TaxationSystem.CalculateIncome(snap.Population, snap.TaxPolicy) + " coins");
            GUILayout.Label("Tax changes apply on the next End Season.");

            GUILayout.Space(8);
            GUILayout.Label("SEASONAL FINANCES");
            if (_lastTurn == null)
            {
                GUILayout.Label("End a season to see actual operating expenses.");
            }
            else
            {
                GUILayout.Label("Last season tax revenue: +" + _lastTurn.TaxIncome);
                GUILayout.Label("Last season operating costs: -" + _lastTurn.OperatingCosts);
                GUILayout.Label("Operating balance: " +
                    (_lastTurn.TaxIncome - _lastTurn.OperatingCosts).ToString("+0;-0;0") +
                    " coins (excluding trade and construction)");
                GUILayout.Label("Employed: " + _lastTurn.WorkingResidents +
                    "  Unemployed: " + _lastTurn.UnemployedResidents);
                GUILayout.Label("Food consumed: " + _lastTurn.FoodConsumed +
                    "  Shortage: " + _lastTurn.FoodShortage);
            }

            if (hall.Id != BuildingCatalog.VillageHallId) return;

            GUILayout.Space(10);
            GUILayout.Label("UPGRADE TO TOWN HALL");
            var upgrade = CivicUpgradeCatalog.VillageHallToTownHall;
            GUILayout.Label("Requires " + upgrade.MinimumPopulation + " residents.");
            GUILayout.Label("Cost: " + upgrade.Cost[Resource.Wood] + " wood, " +
                upgrade.Cost[Resource.Stone] + " stone, " +
                upgrade.Cost[Resource.Coins] + " coins.");
            GUILayout.Label("Duration: " + upgrade.ConstructionSeasons + " seasons.");
            var canAfford = upgrade.Cost.All(entry =>
                snap.Resources.TryGetValue(entry.Key, out var value) &&
                value >= entry.Value);
            var canUpgrade = hall.IsComplete && !hall.IsUpgrading &&
                snap.Population >= upgrade.MinimumPopulation && canAfford;

            originalGuiState = GUI.enabled;
            GUI.enabled = canUpgrade;
            if (GUILayout.Button("Upgrade Village Hall", GUILayout.Height(28)))
            {
                if (_session.UpgradeBuilding(hall.Index))
                {
                    _message = "Town Hall upgrade started.";
                    Refresh();
                }
                else
                    _message = "Upgrade requirements are not met.";
            }
            GUI.enabled = originalGuiState;
            if (hall.IsUpgrading) GUILayout.Label("Upgrade in progress.");
            else if (snap.Population < upgrade.MinimumPopulation)
                GUILayout.Label("Need " + (upgrade.MinimumPopulation - snap.Population) +
                    " more residents.");
            else if (!canAfford) GUILayout.Label("Not enough construction resources.");
        }

        private int ResourceAmount(WorldSnapshot snap, Resource resource)
        {
            return snap.Resources.TryGetValue(resource, out var count) ? count : 0;
        }

        private void ToolButton(Tool option, string title, bool unlocked = true)
        {
            var oldColor = GUI.backgroundColor;
            var wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && unlocked;
            if (_tool == option) GUI.backgroundColor = new Color(.74f, .91f, .7f);
            if (GUILayout.Button(title, GUILayout.Height(25))) _tool = option;
            GUI.backgroundColor = oldColor;
            GUI.enabled = wasEnabled;
        }

        private void TaxButton(TaxPolicy policy, string title, TaxPolicy current)
        {
            var old = GUI.backgroundColor;
            if (policy == current) GUI.backgroundColor = new Color(.74f, .91f, .7f);
            if (GUILayout.Button(title))
            {
                _session.ChangeTaxes(policy);
                _message = "Taxation set to " + title + ".";
                _cachedSnapshot = _session.Snapshot();
            }
            GUI.backgroundColor = old;
        }

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

        private void TradeButtons(Resource resource, string title, int units)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(58));
            if (GUILayout.Button("Buy " + units))
                Trade(TradeDirection.Buy, resource, units);
            if (GUILayout.Button("Sell " + units))
                Trade(TradeDirection.Sell, resource, units);
            GUILayout.EndHorizontal();
        }

        private void Trade(TradeDirection direction, Resource resource, int units)
        {
            var outcome = _session.Trade(_selectedBuilding, direction, resource, units);
            _message = outcome.Succeeded
                ? direction + " " + units + " " + resource + " for " + outcome.TotalCoins + " coins."
                : "Trading failed: " + outcome.Failure;
            if (outcome.Succeeded)
                _cachedSnapshot = _session.Snapshot();
        }
    }
}
