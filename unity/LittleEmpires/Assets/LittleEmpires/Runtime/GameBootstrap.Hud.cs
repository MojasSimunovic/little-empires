using System;
using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleEmpires.Unity
{
    public sealed partial class GameBootstrap
    {
        private IslandHud _hud;
        private static IslandHud _activeHud;

        internal static bool MouseInsideHud() =>
            _activeHud != null && _activeHud.Picks(Input.mousePosition);

        private void OnDestroy()
        {
            if (_activeHud == _hud) _activeHud = null;
            _hud?.Dispose();
        }

        private void SelectTool(Tool tool)
        {
            if (_tool != tool) CancelRoad();
            _tool = tool;
            if (tool != Tool.Inspect) _showInspector = false;
        }

        private sealed partial class IslandHud : IDisposable
        {
            private static readonly string[] Categories = { "Village", "Town", "Roads" };
            private static readonly Tool[][] Tools =
            {
                new[] { Tool.Cottage, Tool.VillageHall, Tool.Farm, Tool.Fishery, Tool.LumberCamp, Tool.Quarry, Tool.Marketplace },
                new[] { Tool.LumberMill, Tool.FoodProcessor, Tool.Stoneworks, Tool.TradingHarbor },
                new[] { Tool.DirtRoad, Tool.StoneRoad }
            };

            private readonly GameBootstrap _game;
            private readonly GameObject _host;
            private readonly VisualElement _root, _cards, _inspector, _tooltip;
            private readonly ScrollView _body;
            private readonly Toggle _roadMode;
            private readonly Label _season, _tier, _population, _happiness, _feedback, _buildingName;
            private readonly Dictionary<Resource, Label> _resources = new();
            private readonly Dictionary<Tool, Button> _buttons = new();
            private readonly List<Button> _tabs = new();
            private WorldSnapshot _snapshot, _inspectedSnapshot;
            private int _category, _inspectedIndex = -1;
            private Tool _shownTool = (Tool)(-1);
            private string _lastMessage;
            private IVisualElementScheduledItem _hover, _hideFeedback;
            private VisualElement _hoverAnchor;

            public IslandHud(GameBootstrap game)
            {
                _game = game;
                _host = new GameObject("Island HUD");
                _host.SetActive(false);
                _host.transform.SetParent(game.transform, false);
                var document = _host.AddComponent<UIDocument>();
                document.panelSettings = Resources.Load<PanelSettings>("Hud/IslandPanel");
                document.visualTreeAsset = Resources.Load<VisualTreeAsset>("Hud/IslandHud");
                _host.SetActive(true);
                document.rootVisualElement.pickingMode = PickingMode.Ignore;
                _root = document.rootVisualElement.Q("hud");
                _root.styleSheets.Add(Resources.Load<StyleSheet>("Hud/IslandHud"));
                _cards = _root.Q("cards");
                _inspector = _root.Q("inspector");
                _tooltip = _root.Q("tooltip");
                _body = _root.Q<ScrollView>("inspector-body");
                _buildingName = _root.Q<Label>("building-name");
                _season = _root.Q<Label>("season");
                _tier = _root.Q<Label>("tier");
                _population = _root.Q<Label>("population");
                _happiness = _root.Q<Label>("happiness");
                _feedback = _root.Q<Label>("feedback");
                _roadMode = _root.Q<Toggle>("road-mode");
                _roadMode.label = "";
                _roadMode.text = "Click start / click end";
                _roadMode.SetValueWithoutNotify(game.clickRoadEndpoints);
                _roadMode.RegisterValueChangedCallback(change =>
                {
                    game.CancelRoad();
                    game.clickRoadEndpoints = change.newValue;
                });
                var endSeason = _root.Q<Button>("end-season");
                endSeason.clicked += () =>
                {
                    endSeason.Blur();
                    game.EndSeason();
                };
                _root.Q<Button>("close-inspector").clicked += () => game._showInspector = false;

                foreach (Resource resource in Enum.GetValues(typeof(Resource)))
                {
                    var block = new VisualElement();
                    block.AddToClassList("row");
                    block.AddToClassList("resource");
                    block.Add(Icon(resource.ToString().ToLowerInvariant(), "resource-icon"));
                    var text = new VisualElement();
                    text.Add(Line(resource.ToString(), "resource-name"));
                    _resources.Add(resource, Line("", "resource-value"));
                    text.Add(_resources[resource]);
                    block.Add(text);
                    _root.Q("stockpiles").Add(block);
                    Hover(block, () => DescribeResource(resource));
                }

                for (var i = 0; i < Categories.Length; i++)
                {
                    var category = i;
                    var tab = Button(Categories[i], () => ShowCategory(category));
                    _tabs.Add(tab);
                    _root.Q("tabs").Add(tab);
                }
                _root.Q("tabs").Add(Button("Inspect", () => game.SelectTool(Tool.Inspect)));
                BindDemolition();
                ShowCategory(0);
                Sync();
            }

            public bool Picks(Vector2 screenPosition)
            {
                if (HasModal) return true;
                if (_root.panel == null) return false;
                var point = RuntimePanelUtils.ScreenToPanel(_root.panel,
                    new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                var element = _root.panel.Pick(point);
                return element != null && _root.Contains(element);
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(_host);
            }

            public void Sync()
            {
                var current = _game._cachedSnapshot;
                if (current == null) return;
                var changed = !ReferenceEquals(_snapshot, current);
                _snapshot = current;
                if (changed)
                {
                    _season.text = $"Year {current.Year}  ·  {current.Season}";
                    _tier.text = current.Tier.ToString().ToUpperInvariant() + " SETTLEMENT";
                    _population.text = $"Residents  {current.Population}/{current.HousingCapacity}";
                    _happiness.text = $"Happiness  {current.Happiness}";
                    foreach (var resource in _resources)
                        resource.Value.text = current.Resources[resource.Key].ToString();
                }
                if (changed || _shownTool != _game._tool)
                {
                    var toolChanged = _shownTool != _game._tool;
                    _shownTool = _game._tool;
                    var category = Array.FindIndex(Tools, group => group.Contains(_shownTool));
                    if (category >= 0 && toolChanged && _category != category)
                        ShowCategory(category);
                    UpdateCards();
                }
                if (_lastMessage != _game._message)
                {
                    _lastMessage = _game._message;
                    _feedback.text = _lastMessage;
                    _feedback.style.display = DisplayStyle.Flex;
                    _hideFeedback?.Pause();
                    _hideFeedback = _feedback.schedule.Execute(() => _feedback.style.display = DisplayStyle.None)
                        .StartingIn(4500);
                }
                SyncInspector();
            }


            private void ShowCategory(int category)
            {
                HideTooltip();
                _category = category;
                for (var i = 0; i < _tabs.Count; i++)
                    _tabs[i].EnableInClassList("selected", i == category);
                _cards.Clear();
                _buttons.Clear();
                foreach (var tool in Tools[category])
                {
                    var definition = Definition(tool);
                    var name = tool == Tool.FoodProcessor ? "Food Workshop" : definition?.Name ??
                        (tool == Tool.DirtRoad ? "Dirt Road" : "Stone Upgrade");
                    var icon = definition?.Id ?? (tool == Tool.DirtRoad ? "dirt-road" : "stone-road");
                    var button = Button("", () =>
                    {
                        if (Blocked(tool) is string reason)
                            _game._message = reason;
                        else
                            _game.SelectTool(tool);
                    });
                    button.AddToClassList("card");
                    button.Add(Icon(icon, "card-image"));
                    button.Add(Line(name, "card-caption"));
                    var badge = Line("", "card-badge");
                    badge.name = "badge";
                    button.Add(badge);
                    _buttons.Add(tool, button);
                    _cards.Add(button);
                    Hover(button, () => DescribeTool(tool));
                }
                if (_snapshot != null) UpdateCards();
            }

            private void UpdateCards()
            {
                _roadMode.style.display = _category == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                foreach (var pair in _buttons)
                {
                    var tool = pair.Key;
                    var definition = Definition(tool);
                    var locked = definition != null && !_game._session.Settlement.IsBuildingUnlocked(definition);
                    var unique = definition != null && !_game._session.Settlement.CanStartBuilding(definition);
                    var unaffordable = definition != null && !_game._session.State.CanAfford(definition.Cost);
                    var button = pair.Value;
                    button.EnableInClassList("locked", locked || unique);
                    button.EnableInClassList("unaffordable", unaffordable && !locked && !unique);
                    button.EnableInClassList("selected", tool == _game._tool);
                    button.Q<Label>("badge").text = locked ? "LOCK" : unique ? "BUILT" : unaffordable ? "!" : "";
                }
            }

            private static BuildingDefinition Definition(Tool tool)
            {
                var id = BuildingIdForTool(tool);
                return id == null ? null : BuildingCatalog.Get(id);
            }

            private string Blocked(Tool tool)
            {
                var definition = Definition(tool);
                if (definition == null) return null;
                if (!_game._session.Settlement.IsBuildingUnlocked(definition)) return "Requires a completed Town Hall.";
                if (!_game._session.Settlement.CanStartBuilding(definition)) return "This settlement already has a Hall.";
                return _game._session.State.CanAfford(definition.Cost) ? null : "Missing " + Missing(definition.Cost) + ".";
            }

            private string Missing(IReadOnlyDictionary<Resource, int> cost) =>
                Amounts(cost.Where(entry => entry.Value > _snapshot.Resources[entry.Key])
                    .Select(entry => new KeyValuePair<Resource, int>(entry.Key, entry.Value - _snapshot.Resources[entry.Key])));

            private static string Amounts(IEnumerable<KeyValuePair<Resource, int>> values) =>
                string.Join("  ·  ", values.Select(entry => $"{entry.Value} {entry.Key.ToString().ToLowerInvariant()}"));

            private static string Benefit(BuildingDefinition building) => building.Id switch
            {
                BuildingCatalog.VillageHallId or BuildingCatalog.TownHallId => "Manage settlement taxes and finances.",
                BuildingCatalog.LumberMillId => $"+{ProductionSystem.LumberMillBonusPercent}% wood from road-connected lumber camps.",
                BuildingCatalog.FoodProcessorId => $"+{ProductionSystem.FoodProcessorBonusPercent}% food from road-connected farms and fisheries.",
                BuildingCatalog.StoneworksId => $"+{ProductionSystem.StoneworksBonusPercent}% stone from road-connected quarries.",
                BuildingCatalog.MarketplaceId or BuildingCatalog.TradingHarborId => "Buy and sell resources through the road network.",
                _ when building.HousingCapacity > 0 => $"Housing for {building.HousingCapacity} residents.",
                _ => $"Base production per season  {Amounts(building.ProductionPerSeason)}"
            };

            private static string Site(BuildingDefinition building) => building.SiteRequirement switch
            {
                PlacementRequirement.ForestEdge => "Level ground at a forest edge",
                PlacementRequirement.Shoreline => "Level shoreline",
                PlacementRequirement.Mountain => "Level mountain terrace",
                PlacementRequirement.FertileLand => "Level fertile land",
                _ => "Level land"
            };

            private void DescribeTool(Tool tool)
            {
                var building = Definition(tool);
                if (building == null)
                {
                    var dirt = tool == Tool.DirtRoad;
                    Detail(_tooltip, dirt ? "Dirt Road" : "Stone Road Upgrade", "heading");
                    Detail(_tooltip, dirt ? "Connect buildings. Free to construct." :
                        $"{IslandRoadNetwork.StoneUpgradeCost} stone per road tile. Bridges cost {_game._session.Island.Roads.StoneBridgeUpgradeCost} stone.");
                    Detail(_tooltip, dirt ? "Drag a route or choose two endpoints. [R]" : "Upgrade an existing route. [T]");
                    return;
                }
                Detail(_tooltip, building.Name, "heading");
                Detail(_tooltip, Benefit(building));
                Detail(_tooltip, $"Build cost  {Amounts(building.Cost)}");
                Detail(_tooltip, $"Construction  {building.ConstructionSeasons} season(s)");
                Detail(_tooltip, $"Workers  {building.WorkersRequired}   ·   Upkeep  {building.UpkeepCoinsPerSeason} coins");
                Detail(_tooltip, $"{Site(building)}  ·  {building.Width}×{building.Height} tiles");
                if (building.RequiredTier == SettlementTier.Town)
                    Detail(_tooltip, "Unlock  Completed Town Hall");
                var reason = Blocked(tool);
                if (reason != null) Detail(_tooltip, reason, "warning");
            }

            private void DescribeResource(Resource resource)
            {
                Detail(_tooltip, resource.ToString(), "heading");
                Detail(_tooltip, $"Stockpile  {_snapshot.Resources[resource]}");
                var turn = _game._lastTurn;
                if (turn == null)
                {
                    Detail(_tooltip, "Seasonal totals appear after the first season.");
                    return;
                }
                Detail(_tooltip, $"Last season net  {turn.ResourceChanges[resource]:+0;-0;0}");
                if (turn.GrossProduction.TryGetValue(resource, out var produced))
                    Detail(_tooltip, $"Produced  {produced}");
                if (resource == Resource.Food) Detail(_tooltip, $"Consumed  {turn.FoodConsumed}");
                if (resource == Resource.Coins)
                    Detail(_tooltip, $"Taxes  +{turn.TaxIncome}   ·   Upkeep  −{turn.OperatingCosts}");
            }

            private void Hover(VisualElement anchor, Action describe)
            {
                anchor.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    HideTooltip();
                    _hoverAnchor = anchor;
                    _hover = anchor.schedule.Execute(() =>
                    {
                        if (_hoverAnchor != anchor) return;
                        _tooltip.Clear();
                        describe();
                        _tooltip.style.display = DisplayStyle.Flex;
                        var bounds = anchor.worldBound;
                        var root = _root.worldBound;
                        var fromTop = bounds.center.y < root.center.y;
                        var dockTop = _root.Q("build-dock").worldBound.yMin;
                        _tooltip.style.left = Mathf.Clamp(bounds.xMin - root.xMin, 8, Mathf.Max(8, root.width - 288));
                        _tooltip.style.top = fromTop ? new StyleLength(bounds.yMax - root.yMin + 7) : new StyleLength(StyleKeyword.Auto);
                        _tooltip.style.bottom = fromTop ? new StyleLength(StyleKeyword.Auto) : new StyleLength(root.yMax - dockTop + 7);
                    }).StartingIn(330);
                });
                anchor.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoverAnchor == anchor) HideTooltip();
                });
            }

            private void HideTooltip()
            {
                _hover?.Pause();
                _hoverAnchor = null;
                _tooltip.style.display = DisplayStyle.None;
            }

            private static VisualElement Icon(string id, string style)
            {
                var image = new VisualElement { pickingMode = PickingMode.Ignore };
                image.AddToClassList(style);
                var texture = Resources.Load<Texture2D>("Hud/Icons/" + id);
                if (texture != null) image.style.backgroundImage = new StyleBackground(texture);
                return image;
            }

            private static Label Line(string text, string style)
            {
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList(style);
                return label;
            }

            private static void Detail(VisualElement parent, string text, string style = "detail") =>
                parent.Add(Line(text, style));

            private static Button Button(string text, Action clicked)
            {
                var button = new Button { text = text };
                button.clicked += () =>
                {
                    button.Blur();
                    clicked();
                };
                return button;
            }
        }
    }
}
