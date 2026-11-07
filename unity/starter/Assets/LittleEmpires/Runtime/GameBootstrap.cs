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

        private WorldGameSession _session;
        private IslandView _view;
        private Vector2 _menuScroll;
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

        private void Start()
        {
            _session = WorldGameSession.NewStrategicIsland(islandSeed);
            var visualRoot = new GameObject("World visuals");
            _view = visualRoot.AddComponent<IslandView>();
            _view.Rebuild(_session.Snapshot());

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200f;
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            var controller = camera.GetComponent<IslandCamera>();
            if (controller == null) controller = camera.gameObject.AddComponent<IslandCamera>();
            controller.SetTarget(new Vector3(0, .2f, 0));

            if (FindFirstObjectByType<Light>() == null)
            {
                var sunObject = new GameObject("Warm afternoon sun");
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, .91f, .76f);
                sun.intensity = 1.25f;
                sun.transform.rotation = Quaternion.Euler(53f, -37f, 0f);
            }

            RenderSettings.ambientLight = new Color(.64f, .69f, .67f);
            _message = "Unity is reading the real C# island! Select any tool to begin.";
        }

        private void Update()
        {
            if (_session == null) return;

            if (Input.GetKeyDown(KeyCode.R)) _tool = Tool.DirtRoad;
            if (Input.GetKeyDown(KeyCode.T)) _tool = Tool.StoneRoad;
            if (Input.GetKeyDown(KeyCode.E)) EndSeason();
            if (Input.GetKeyDown(KeyCode.Escape)) _tool = Tool.Inspect;

            if (!Input.GetMouseButtonDown(0) || MouseInsideHud()) return;
            var camera = Camera.main;
            if (camera == null) return;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 250f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var tile = hit.collider.GetComponent<IslandTileTarget>();
                if (tile == null) continue;
                ClickTile(tile.Position);
                return;
            }
        }

        private bool MouseInsideHud()
        {
            var point = Input.mousePosition;
            var x = point.x;
            var yFromTop = Screen.height - point.y;
            return (x < 290 && yFromTop > 0) ||
                   (x > Screen.width - 320 && yFromTop > 0 && yFromTop < 420);
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
            GUILayout.BeginArea(new Rect(12, 12, 275, Screen.height - 24), GUI.skin.box);
            _menuScroll = GUILayout.BeginScrollView(_menuScroll);

            GUILayout.Label("LITTLE EMPIRES   •   UNITY");
            GUILayout.Label("Year " + snap.Year + " — " + snap.Season);
            GUILayout.Label("Residents " + snap.Population + "/" + snap.HousingCapacity +
                "   Happiness " + snap.Happiness);
            GUILayout.Space(5);
            GUILayout.Label("WOOD " + ResourceAmount(snap, Resource.Wood) +
                "   STONE " + ResourceAmount(snap, Resource.Stone));
            GUILayout.Label("FOOD " + ResourceAmount(snap, Resource.Food) +
                "   COINS " + ResourceAmount(snap, Resource.Coins));
            GUILayout.Space(8);

            if (GUILayout.Button("END SEASON   [E]", GUILayout.Height(35)))
                EndSeason();

            GUILayout.Space(6);
            GUILayout.Label("BUILDINGS");
            ToolButton(Tool.Cottage, "Cottage");
            ToolButton(Tool.Farm, "Farm");
            ToolButton(Tool.Fishery, "Fishery");
            ToolButton(Tool.LumberCamp, "Lumber Camp");
            ToolButton(Tool.Quarry, "Quarry");
            ToolButton(Tool.LumberMill, "Lumber Mill");
            ToolButton(Tool.FoodProcessor, "Food Processing Plant");
            ToolButton(Tool.Marketplace, "Marketplace");

            GUILayout.Space(6);
            GUILayout.Label("INFRASTRUCTURE");
            ToolButton(Tool.DirtRoad, "Dirt road (free) [R]");
            ToolButton(Tool.StoneRoad, "Upgrade to stone (2 stone) [T]");
            ToolButton(Tool.UpgradeHousing, "Cottage > Townhouse");
            ToolButton(Tool.Inspect, "Inspect / select [Esc]");

            GUILayout.Space(6);
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
            GUILayout.Label("Right drag: orbit   •   Scroll: zoom");
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            var rightX = Mathf.Max(295, Screen.width - 315);
            GUILayout.BeginArea(new Rect(rightX, 12, 303, 405), GUI.skin.box);
            GUILayout.Label("SETTLEMENT INSPECTOR");
            GUILayout.Label(_message, GUI.skin.box);

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
                    GUILayout.Label("MARKETPLACE — 5 units per order");
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

            GUILayout.EndArea();
        }

        private int ResourceAmount(WorldSnapshot snap, Resource resource)
        {
            return snap.Resources.TryGetValue(resource, out var count) ? count : 0;
        }

        private void ToolButton(Tool option, string title)
        {
            var old = GUI.backgroundColor;
            if (_tool == option) GUI.backgroundColor = new Color(.74f, .91f, .7f);
            if (GUILayout.Button(title, GUILayout.Height(27))) _tool = option;
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
