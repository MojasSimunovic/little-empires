using System;
using System.Linq;
using LittleEmpires.Core;

public static class CivicHallTests
{
    public static void Run(Action<bool, string> check)
    {
        var villageDef = BuildingCatalog.Get(BuildingCatalog.VillageHallId);
        var townDef = BuildingCatalog.Get(BuildingCatalog.TownHallId);
        villageDef.Validate();
        townDef.Validate();
        check(villageDef.CanBuildDirectly && !townDef.CanBuildDirectly &&
              villageDef.HousingCapacity == 0 && townDef.HousingCapacity == 0,
            "Village Hall is buildable; Town Hall is upgrade-only");

        var session = WorldGameSession.NewStrategicIsland(42);
        var sites = session.Island.Tiles.Where(tile =>
            BuildingPlacementRules.Evaluate(session.Island, tile.Position, villageDef).Allowed)
            .Take(2).ToArray();
        check(sites.Length == 2, "Village Hall has valid sites");
        check(session.PlaceBuilding(sites[0].Position, BuildingCatalog.VillageHallId),
            "Village Hall placement begins");
        var afterPurchase = session.State.Get(Resource.Coins);
        check(!session.PlaceBuilding(sites[1].Position, BuildingCatalog.VillageHallId) &&
              session.State.Get(Resource.Coins) == afterPurchase,
            "Duplicate Village Hall cannot be placed or paid for");
        check(!session.PlaceBuilding(sites[1].Position, BuildingCatalog.TownHallId),
            "Town Hall cannot be built directly");
        check(!session.UpgradeBuilding(0), "Unfinished Hall cannot upgrade");
        session.EndSeason();
        check(session.Snapshot().Buildings[0].IsComplete, "Village Hall completes in one season");
        var lowPopCoins = session.State.Get(Resource.Coins);
        check(!session.UpgradeBuilding(0) && session.State.Get(Resource.Coins) == lowPopCoins,
            "Population gate rejects Hall upgrade without a charge");

        var upgrade = CivicUpgradeCatalog.VillageHallToTownHall;
        upgrade.Validate();
        check(upgrade.MinimumPopulation == 50 && upgrade.ConstructionSeasons == 2 &&
              upgrade.Cost[Resource.Coins] == 300, "Civic threshold and costs are configured");

        var treasury = new GameState(wood: 200, stone: 80, food: 600, coins: 900);
        var mature = new Settlement(population: 50, economyMode: EconomyMode.Strategic);
        check(mature.TryStartConstruction(treasury, BuildingCatalog.VillageHallId),
            "Mature settlement starts its Hall");
        var engine = new TurnEngine();
        engine.EndTurn(treasury, mature);
        mature.SetTaxPolicy(TaxPolicy.High);
        var sameBuilding = mature.Buildings[0];
        // Test the exact boundary: 49 is not a Town; 50 is.
        mature.SetPopulation(49);
        var coinsBeforeThreshold = treasury.Get(Resource.Coins);
        check(!mature.TryStartUpgrade(treasury, 0) &&
              treasury.Get(Resource.Coins) == coinsBeforeThreshold,
            "49 residents cannot start a Town Hall upgrade or lose coins");
        mature.SetPopulation(50);
        check(mature.TryStartUpgrade(treasury, 0) &&
              treasury.Get(Resource.Wood) == 105 &&
              treasury.Get(Resource.Stone) == 40 &&
              treasury.Get(Resource.Coins) == 570,
            "Mature Hall upgrade charges exactly once");
        check(sameBuilding.IsUpgrading && sameBuilding.UpgradeSeasonsRemaining == 2 &&
              sameBuilding.Definition.Id == BuildingCatalog.VillageHallId,
            "Building identity and Village Hall state persist during upgrade");
        var first = engine.EndTurn(treasury, mature);
        check(first.TaxIncome == 75 && first.CompletedUpgrades.Count == 0 &&
              sameBuilding.UpgradeSeasonsRemaining == 1,
            "Taxes continue during construction");
        var second = engine.EndTurn(treasury, mature);
        check(second.CompletedUpgrades.Single() == "Village Hall → Town Hall" &&
              ReferenceEquals(sameBuilding, mature.Buildings[0]) &&
              sameBuilding.Definition.Id == BuildingCatalog.TownHallId &&
              mature.TaxPolicy == TaxPolicy.High && mature.HousingCapacity == 50,
            "Hall upgrade keeps the building, housing and tax policy");
        check(!mature.TryStartUpgrade(treasury, 0), "Upgraded Hall cannot upgrade again");
    }
}
