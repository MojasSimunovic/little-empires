using System;
using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;
using UnityEngine.UIElements;

namespace LittleEmpires.Unity
{
    public sealed partial class GameBootstrap
    {
        private sealed partial class IslandHud
        {
            private void SyncInspector()
            {
                var index = _game._showInspector ? _game._selectedBuilding : -1;
                if (_inspectedIndex == index && ReferenceEquals(_inspectedSnapshot, _snapshot)) return;
                _inspectedIndex = index;
                _inspectedSnapshot = _snapshot;
                if (index < 0 || index >= _snapshot.Buildings.Count)
                {
                    _inspector.style.display = DisplayStyle.None;
                    return;
                }
                _inspector.style.display = DisplayStyle.Flex;
                _body.Clear();
                var building = _snapshot.Buildings[index];
                var definition = BuildingCatalog.Get(building.Id);
                _buildingName.text = building.Name;
                Detail(_body, building.IsUpgrading ? $"Upgrading · {building.UpgradeSeasonsRemaining} season(s) remaining" :
                    building.IsComplete ? "Operational" : $"Under construction · {building.SeasonsRemaining} season(s) remaining",
                    "eyebrow");
                Detail(_body, Benefit(definition));
                if (building.WorkersRequired > 0)
                {
                    Detail(_body, $"Workforce  ·  {building.WorkersRequired} workers", "section");
                    var row = Row(_body);
                    Detail(row, $"Priority {building.WorkPriority}", "grow");
                    row.Add(Button("−", () => _game.ChangePriority(index, building.WorkPriority - 1)));
                    row.Add(Button("+", () => _game.ChangePriority(index, building.WorkPriority + 1)));
                }

                if (IsCivicHall(building.Id)) Administration(building);
                if (building.IsComplete &&
                    (building.Id == BuildingCatalog.MarketplaceId || building.Id == BuildingCatalog.TradingHarborId))
                    Trading(building);
                if (BuildingUpgradeCatalog.TryGet(building.Id, out var upgrade))
                    Upgrade(building, upgrade);
            }

            private void Administration(BuildingView hall)
            {
                Detail(_body, "Settlement", "section");
                Detail(_body, $"{_snapshot.Tier}  ·  Residents {_snapshot.Population}/{_snapshot.HousingCapacity}");
                Detail(_body, $"Happiness {_snapshot.Happiness}  ·  Treasury {_snapshot.Resources[Resource.Coins]} coins");
                Detail(_body, Amounts(new[] { Resource.Wood, Resource.Stone, Resource.Food }
                    .Select(resource => new KeyValuePair<Resource, int>(resource, _snapshot.Resources[resource]))));
                Detail(_body, "Tax policy", "section");
                var row = Row(_body);
                foreach (TaxPolicy policy in Enum.GetValues(typeof(TaxPolicy)))
                {
                    var title = policy == TaxPolicy.VeryHigh ? "Very high" : policy.ToString();
                    var button = Button(title, () =>
                    {
                        _game._session.ChangeTaxes(policy);
                        _game._cachedSnapshot = _game._session.Snapshot();
                        _game._message = $"Tax policy set to {title}.";
                    });
                    button.EnableInClassList("selected", policy == _snapshot.TaxPolicy);
                    button.SetEnabled(hall.IsComplete);
                    row.Add(button);
                }

                var effects = TaxationSystem.Effects(_snapshot.TaxPolicy);
                Detail(_body, $"Tax happiness  {effects.HappinessPerSeason:+0;-0;0} per season");
                Detail(_body, $"Next-season tax estimate  +{TaxationSystem.CalculateIncome(_snapshot.Population, _snapshot.TaxPolicy)} coins");
                Detail(_body, "Last season", "section");
                var turn = _game._lastTurn;
                if (turn == null)
                {
                    Detail(_body, "End a season to see actual income and expenses.");
                    return;
                }
                Detail(_body, $"Taxes  +{turn.TaxIncome}  ·  Upkeep  −{turn.OperatingCosts}");
                Detail(_body, $"Operating balance  {turn.TaxIncome - turn.OperatingCosts:+0;-0;0} coins");
                Detail(_body, $"Employed {turn.WorkingResidents}  ·  Unemployed {turn.UnemployedResidents}");
                Detail(_body, $"Food consumed {turn.FoodConsumed}  ·  Shortage {turn.FoodShortage}");
            }

            private void Trading(BuildingView building)
            {
                var harbor = building.Id == BuildingCatalog.TradingHarborId;
                var capacity = harbor ? MarketplaceTrading.MaxUnitsPerHarborPerSeason :
                    MarketplaceTrading.MaxUnitsPerMarketPerSeason;
                var units = harbor ? 20 : 5;
                Detail(_body, "Trading", "section");
                Detail(_body, $"Seasonal quota  {building.UnitsTradedThisSeason}/{capacity} units");
                foreach (var resource in new[] { Resource.Wood, Resource.Stone, Resource.Food })
                {
                    var row = Row(_body);
                    Detail(row, resource.ToString(), "grow");
                    row.Add(Button($"Buy {units}", () => _game.Trade(TradeDirection.Buy, resource, units)));
                    row.Add(Button($"Sell {units}", () => _game.Trade(TradeDirection.Sell, resource, units)));
                }
            }

            private void Upgrade(BuildingView building, BuildingUpgradeDefinition upgrade)
            {
                var destination = BuildingCatalog.Get(upgrade.ToBuildingId);
                Detail(_body, $"Upgrade to {destination.Name}", "section");
                Detail(_body, $"{upgrade.MinimumPopulation} residents  ·  {upgrade.ConstructionSeasons} seasons");
                Detail(_body, Amounts(upgrade.Cost));
                var missing = Missing(upgrade.Cost);
                var reason = !building.IsComplete ? "Complete construction first." :
                    building.IsUpgrading ? "Upgrade in progress." :
                    _snapshot.Population < upgrade.MinimumPopulation ?
                        $"Need {upgrade.MinimumPopulation - _snapshot.Population} more residents." :
                    missing.Length > 0 ? $"Missing {missing}." : null;
                if (reason != null) Detail(_body, reason, "warning");
                var button = Button($"Upgrade to {destination.Name}", () =>
                {
                    var success = _game._session.UpgradeBuilding(building.Index);
                    _game._message = success ? $"{destination.Name} upgrade started." : "Upgrade requirements are unmet.";
                    if (success) _game.Refresh();
                });
                button.SetEnabled(reason == null);
                _body.Add(button);
            }

            private static VisualElement Row(VisualElement parent)
            {
                var row = new VisualElement();
                row.AddToClassList("row");
                parent.Add(row);
                return row;
            }
        }
    }
}
