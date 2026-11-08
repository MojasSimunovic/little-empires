using System;
using System.Linq;
using LittleEmpires.Core;

public static class DemolitionTests
{
    public static void Run(Action<bool, string> check)
    {
        var island = new IslandMap(10, 10, new GridPosition(0, 0).Rectangle(10, 10)
            .Select(p => new IslandTile(p, p == new GridPosition(4, 2) ? TerrainKind.FertileLand : TerrainKind.Grassland)));
        var game = new WorldGameSession(new GameState(1000, 1000, 1000, 1000),
            new Settlement(50, economyMode: EconomyMode.Strategic), island);
        var cottage = new GridPosition(2, 2);
        var farm = new GridPosition(4, 2);
        var market = new GridPosition(6, 2);
        var hall = new GridPosition(2, 5);
        check(game.PlaceBuilding(cottage, BuildingCatalog.CottageId) &&
              game.PlaceBuilding(farm, BuildingCatalog.FarmId) &&
              game.PlaceBuilding(market, BuildingCatalog.MarketplaceId) &&
              game.PlaceBuilding(hall, BuildingCatalog.VillageHallId),
            "Demolition fixtures place cottage, farm, market and civic Hall");
        var initial = game.Snapshot();
        var cottageId = initial.Buildings[0].InstanceId;
        var farmId = initial.Buildings[1].InstanceId;
        var marketId = initial.Buildings[2].InstanceId;
        var hallId = initial.Buildings[3].InstanceId;
        check(game.PreviewDemolition(cottageId).Succeeded && game.Snapshot().Buildings.Count == 4,
            "Demolition preview has no side effects");
        check(game.DemolishBuilding(cottageId).Succeeded && !island.OccupiedSites.ContainsKey(cottage) &&
              initial.Resources.All(pair => game.State.Get(pair.Key) == pair.Value),
            "Unfinished Cottage frees its plot without a refund");
        check(game.PlaceBuilding(cottage, BuildingCatalog.CottageId) &&
              island.OccupiedSites[cottage].InstanceId != cottageId, "Rebuilding a plot gives it a fresh identity");
        var replacement = island.OccupiedSites[cottage].InstanceId;
        for (var x = 2; x <= 6; x++) game.PlaceDirtRoad(new GridPosition(x, 3));
        game.EndSeason();
        game.EndSeason();
        var working = game.EndSeason();
        check(working.WorkingResidents > 0 && working.GrossProduction[Resource.Food] > 0,
            "Completed Farm employs workers and produces food before demolition");
        check(game.DemolishBuilding(farmId).Succeeded && !island.OccupiedSites.ContainsKey(farm) &&
              game.Snapshot().Roads.Count == 5 && game.Snapshot().Buildings.Any(b => b.InstanceId == marketId),
            "Removing a middle building leaves roads and the market intact");
        check(!game.ChangeWorkPriority(farmId, 3) && !game.UpgradeBuilding(farmId) &&
              game.DemolishBuilding(farmId).Failure == DemolitionFailure.BuildingNotFound &&
              game.Trade(marketId, TradeDirection.Buy, Resource.Wood, 5).Succeeded,
            "Stale IDs cannot retarget commands and surviving market still trades");
        var after = game.EndSeason();
        check(after.WorkingResidents == 0 && !after.GrossProduction.ContainsKey(Resource.Food),
            "Removed Farm contributes no workers or production next season");
        game.Settlement.SetPopulation(55);
        var blocked = game.DemolishBuilding(replacement);
        check(blocked.Failure == DemolitionFailure.InsufficientHousing && blocked.HousingShortfall == 5 &&
              island.OccupiedSites[cottage].InstanceId == replacement && game.Settlement.HousingCapacity == 56,
            "Occupied housing cannot be demolished when the residents would lose capacity");
        check(game.PlaceBuilding(new GridPosition(2, 1), BuildingCatalog.CottageId), "Replacement housing can be built");
        game.EndSeason();
        game.Settlement.SetPopulation(55);
        check(game.UpgradeBuilding(replacement), "Cottage can start an upgrade before demolition");
        var paid = game.Snapshot().Resources;
        check(game.DemolishBuilding(replacement).Succeeded && game.Settlement.HousingCapacity == 56 &&
              game.Settlement.Population == 55 && paid.All(pair => game.State.Get(pair.Key) == pair.Value),
            "Demolishing upgrading housing preserves population with sufficient capacity and gives no refund");
        check(game.UpgradeBuilding(hallId), "Existing Hall keeps its identity after middle removals");
        game.EndSeason();
        game.EndSeason();
        check(game.Settlement.Tier == SettlementTier.Town &&
              game.PlaceBuilding(new GridPosition(7, 5), BuildingCatalog.LumberMillId),
            "Town Hall unlocks Town construction");
        game.ChangeTaxes(TaxPolicy.High);
        check(game.DemolishBuilding(hallId).Succeeded &&
              hall.Rectangle(2, 2).All(p => !island.OccupiedSites.ContainsKey(p)) &&
              game.Settlement.Tier == SettlementTier.Village &&
              game.Settlement.TaxPolicy == TaxPolicy.High &&
              game.Snapshot().Buildings.Any(b => b.Id == BuildingCatalog.LumberMillId) &&
              game.PreviewBuilding(new GridPosition(7, 6), BuildingCatalog.LumberMillId).Failure == PlacementFailure.RequiresSettlementTier,
            "Town Hall demolition releases all four plots, restores Village restrictions and retains policy and existing buildings");
        check(game.PlaceBuilding(hall, BuildingCatalog.VillageHallId) &&
              game.DemolishBuilding(island.OccupiedSites[hall].InstanceId).Succeeded,
            "A new Village Hall can be founded on the entire released footprint and canceled");
    }
}
