using System;
using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;

/// <summary>Village/Town gates, unlock timing, boosters and harbor trade.</summary>
public static class TownProgressionTests
{
    public static void Run(Action<bool, string> check)
    {
        TestTownLocks(check);
        TestCottageGate(check);
        TestTownUpgradeAndHarbor(check);
        TestStoneworks(check);
    }

    private static IslandMap World()
    {
        var tiles = new List<IslandTile>();
        for (var z = 0; z < 9; z++)
        for (var x = 0; x < 13; x++)
        {
            var terrain = (x, z) switch
            {
                (9, 1) => TerrainKind.Water,
                (10, 2) => TerrainKind.Mountain,
                _ => TerrainKind.Grassland
            };
            tiles.Add(new IslandTile(new GridPosition(x, z), terrain));
        }
        return new IslandMap(13, 9, tiles);
    }

    private static void TestTownLocks(Action<bool, string> check)
    {
        var game = WorldGameSession.NewStrategicIsland(42);
        var village = game.Snapshot();
        check(village.Tier == SettlementTier.Village,
            "Fresh strategic settlement starts at Village tier");
        var townBuildings = new[]
        {
            BuildingCatalog.LumberMillId,
            BuildingCatalog.FoodProcessorId,
            BuildingCatalog.StoneworksId,
            BuildingCatalog.TradingHarborId
        };
        var stone = game.State.Get(Resource.Stone);
        var wood = game.State.Get(Resource.Wood);
        var coins = game.State.Get(Resource.Coins);
        var center = new GridPosition(6, 6);
        foreach (var buildingId in townBuildings)
        {
            var definition = BuildingCatalog.Get(buildingId);
            check(definition.RequiredTier == SettlementTier.Town &&
                  game.PreviewBuilding(center, buildingId).Failure ==
                      PlacementFailure.RequiresSettlementTier &&
                  !game.PlaceBuilding(center, buildingId),
                buildingId + " blocked before Town Hall upgrade");
        }
        check(game.Settlement.Buildings.Count == 0 &&
              game.State.Get(Resource.Wood) == wood &&
              game.State.Get(Resource.Stone) == stone &&
              game.State.Get(Resource.Coins) == coins,
            "Locked industry attempts do not occupy land or spend resources");

        var classic = new Settlement(economyMode: EconomyMode.Classic);
        check(classic.TryStartConstruction(new GameState(), new BuildingDefinition(
                "legacy-processor", "Legacy Processor",
                new Dictionary<Resource, int>(), 1,
                new Dictionary<Resource, int>(),
                RequiredTier: SettlementTier.Town)),
            "Classic scenario fixtures retain legacy construction access");
    }

    private static void TestCottageGate(Action<bool, string> check)
    {
        var state = new GameState(wood: 100, stone: 100, food: 100, coins: 100);
        var village = new Settlement(population: 19, economyMode: EconomyMode.Strategic);
        var upgrade = HousingUpgradeCatalog.CottageToTownhouse;
        check(upgrade.MinimumPopulation == 20,
            "Cottage upgrade needs 20 residents");
        check(village.TryStartConstruction(state, BuildingCatalog.CottageId),
            "19-resident Village may still build a Cottage");
        new TurnEngine().EndTurn(state, village);
        village.SetPopulation(19); // keep the threshold test deterministic
        var coins = state.Get(Resource.Coins);
        check(!village.TryStartUpgrade(state, 0) && state.Get(Resource.Coins) == coins,
            "19 residents cannot upgrade Cottage or lose coins");
        village.SetPopulation(20);
        check(village.TryStartUpgrade(state, 0),
            "Cottage upgrades unlock at 20 residents");
    }

    private static void TestTownUpgradeAndHarbor(Action<bool, string> check)
    {
        var map = World();
        var state = new GameState(wood: 600, stone: 300, food: 1000, coins: 1500);
        var settlement = new Settlement(population: 50, economyMode: EconomyMode.Strategic);
        var game = new WorldGameSession(state, settlement, map);
        check(game.PlaceBuilding(new GridPosition(2, 2), BuildingCatalog.VillageHallId),
            "Town progress fixture can place a Village Hall");
        game.EndSeason();
        check(game.Snapshot().Tier == SettlementTier.Village &&
              game.UpgradeBuilding(0), "50 residents allow civic upgrade, not immediate unlock");
        game.EndSeason();
        check(game.Snapshot().Tier == SettlementTier.Village,
            "Town buildings remain locked during first upgrade season");
        game.EndSeason();
        check(game.Snapshot().Tier == SettlementTier.Town &&
              game.Snapshot().Buildings[0].Id == BuildingCatalog.TownHallId,
            "Completing the Town Hall upgrades the settlement tier");

        var shore = new GridPosition(9, 2);
        var inland = new GridPosition(5, 5);
        check(game.PreviewBuilding(inland, BuildingCatalog.TradingHarborId).Failure ==
                  PlacementFailure.RequiresShoreline,
            "Trading Harbor is restricted to shoreline land");
        check(game.PreviewBuilding(shore, BuildingCatalog.TradingHarborId).Allowed,
            "Trading Harbor unlocks at Town tier on shoreline");
        var beforeCoins = state.Get(Resource.Coins);
        check(!game.PlaceBuilding(inland, BuildingCatalog.TradingHarborId) &&
              state.Get(Resource.Coins) == beforeCoins,
            "Invalid inland harbor placement does not spend coins");
        check(game.PlaceBuilding(new GridPosition(7, 2), BuildingCatalog.CottageId) &&
              game.PlaceBuilding(shore, BuildingCatalog.TradingHarborId),
            "Town may build housing and a Trading Harbor");
        check(game.Trade(2, TradeDirection.Buy, Resource.Wood, 20).Failure ==
                  TradeFailure.InvalidMarket,
            "Unfinished Trading Harbor cannot trade");
        for (var season = 0; season < 3; season++) game.EndSeason();
        check(game.Trade(2, TradeDirection.Buy, Resource.Wood, 20).Failure ==
                  TradeFailure.NotConnected,
            "Disconnected Trading Harbor cannot trade");
        for (var x = 7; x <= 9; x++)
            check(map.Roads.PlaceDirt(new GridPosition(x, 3)).Succeeded,
                "Harbor road link tile " + x + " built");
        var woodBefore = state.Get(Resource.Wood);
        var buy = game.Trade(2, TradeDirection.Buy, Resource.Wood, 40);
        check(buy.Succeeded && buy.TotalCoins == 200 &&
              state.Get(Resource.Wood) == woodBefore + 40,
            "Harbor can import 40 goods with the existing price spread");
        var sell = game.Trade(2, TradeDirection.Sell, Resource.Food, 80);
        check(sell.Succeeded && sell.TotalCoins == 160 &&
              game.Trade(2, TradeDirection.Sell, Resource.Stone, 1).Failure ==
                  TradeFailure.SeasonalLimit,
            "Harbor can trade 120 total units per season, unlike 30-unit Marketplace");
        check(game.Snapshot().Buildings[2].UnitsTradedThisSeason == 120,
            "Harbor seasonal quota is visible in the immutable snapshot");
        game.EndSeason();
        check(game.Trade(2, TradeDirection.Sell, Resource.Stone, 1).Succeeded,
            "Harbor quota resets next season");
        settlement.SetPopulation(49);
        check(game.Snapshot().Tier == SettlementTier.Town,
            "Town privileges persist after a population dip");
    }

    private static void TestStoneworks(Action<bool, string> check)
    {
        // Classic bypasses unlocks for legacy economic fixtures; the same
        // Stoneworks bonus implementation is used in the real Strategic game.
        var map = World();
        var state = new GameState(wood: 300, stone: 200, food: 0, coins: 500);
        var town = new Settlement(economyMode: EconomyMode.Classic);
        check(town.TryStartConstruction(state, map,
                  new GridPosition(10, 2), BuildingCatalog.QuarryId) &&
              town.TryStartConstruction(state, map,
                  new GridPosition(10, 4), BuildingCatalog.StoneworksId),
            "Quarry and Stoneworks can be placed on their intended terrain");
        var engine = new TurnEngine();
        engine.EndTurn(state, town, map);
        engine.EndTurn(state, town, map);
        var unlinked = engine.EndTurn(state, town, map);
        check(unlinked.GrossProduction[Resource.Stone] == 4,
            "Unconnected Stoneworks does not boost a Quarry");
        check(map.Roads.PlaceDirt(new GridPosition(10, 3)).Succeeded,
            "Quarry and Stoneworks can connect with a shared road");
        var connected = engine.EndTurn(state, town, map);
        check(connected.GrossProduction[Resource.Stone] == 6,
            "Road-connected Stoneworks boosts Quarry stone by fifty percent");
    }
}
