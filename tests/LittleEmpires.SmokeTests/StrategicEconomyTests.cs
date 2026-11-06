using LittleEmpires.Core;

/// <summary>
/// Separate regression suite for the optional Strategic rules. Classic scenarios
/// in Program.cs must continue passing unchanged.
/// </summary>
public static class StrategicEconomyTests
{
    public static void Run(Action<bool, string> check)
    {
        TestTaxes(check);
        TestWorkforce(check);
        TestProductionChains(check);
        TestMarket(check);
    }

    private static IslandMap World()
    {
        var tiles = new List<IslandTile>();
        for (var z = 0; z < 9; z++)
        for (var x = 0; x < 13; x++)
        {
            var terrain = (x, z) switch
            {
                (1, 1) => TerrainKind.Forest,
                (9, 1) => TerrainKind.Water,
                (10, 2) => TerrainKind.Mountain,
                (6, 4) => TerrainKind.FertileLand,
                _ => TerrainKind.Grassland
            };
            tiles.Add(new IslandTile(new GridPosition(x, z), terrain));
        }
        return new IslandMap(13, 9, tiles);
    }

    private static void RoadLine(IslandMap map, int z, int first, int last)
    {
        for (var x = first; x <= last; x++)
            if (!map.Roads.PlaceDirt(new GridPosition(x, z)).Succeeded)
                throw new Exception($"Failed to place test road at {x},{z}.");
    }

    private static void TestTaxes(Action<bool, string> check)
    {
        check(TaxationSystem.CalculateIncome(5, TaxPolicy.Low) == 2 &&
              TaxationSystem.CalculateIncome(5, TaxPolicy.Normal) == 5 &&
              TaxationSystem.CalculateIncome(5, TaxPolicy.High) == 7 &&
              TaxationSystem.CalculateIncome(5, TaxPolicy.VeryHigh) == 10,
            "Low/normal/high/very-high tax brackets have deterministic integer income");

        var classic = new Settlement(population: 8);
        var classicState = new GameState(coins: 0, food: 20);
        classic.SetTaxPolicy(TaxPolicy.VeryHigh);
        var classicTurn = new TurnEngine().EndTurn(classicState, classic);
        check(classicTurn.TaxIncome == 0 && classicState.Get(Resource.Coins) == 0 &&
              classicTurn.Happiness == 71,
            "Classic settlement retains original tax-free balance");

        var village = new Settlement(population: 8,
            economyMode: EconomyMode.Strategic);
        var state = new GameState(coins: 0, food: 100);
        village.SetTaxPolicy(TaxPolicy.High);
        var turn = new TurnEngine().EndTurn(state, village);
        check(turn.TaxIncome == 12 && state.Get(Resource.Coins) == 12 &&
              turn.HappinessChange == -3 && village.Happiness == 67,
            "Strategic high taxes collect coins and reduce happiness");
        village.SetTaxPolicy(TaxPolicy.Low);
        var next = new TurnEngine().EndTurn(state, village);
        check(next.TaxIncome == 4 && next.HappinessChange == 3 &&
              state.Get(Resource.Coins) == 16,
            "Low taxes collect less and improve happiness");
        try
        {
            village.SetTaxPolicy((TaxPolicy)999);
            throw new Exception("Invalid tax policy accepted");
        }
        catch (ArgumentOutOfRangeException)
        {
            check(true, "Invalid tax policy is rejected");
        }

        var highTaxVillage = new Settlement(population: 8,
            economyMode: EconomyMode.Strategic);
        var highTaxState = new GameState(food: 100);
        check(highTaxVillage.TryStartConstruction(
                highTaxState, BuildingCatalog.CottageId),
            "High-tax immigration test starts cottage");
        highTaxVillage.SetTaxPolicy(TaxPolicy.VeryHigh);
        var highTaxTurn = new TurnEngine().EndTurn(highTaxState, highTaxVillage);
        check(highTaxTurn.Happiness == 65 && highTaxTurn.NewResidents == 1,
            "Very high taxes slow immigration despite food and housing");
    }

    private static void TestWorkforce(Action<bool, string> check)
    {
        var state = new GameState(wood: 200, stone: 100, food: 100, coins: 1000);
        var town = new Settlement(population: 8, economyMode: EconomyMode.Strategic);
        check(town.TryStartConstruction(state, BuildingCatalog.FisheryId) &&
              town.TryStartConstruction(state, BuildingCatalog.FarmId) &&
              town.TryStartConstruction(state, BuildingCatalog.LumberCampId),
            "Worker test starts Fishery, Farm and Lumber Camp");
        var engine = new TurnEngine();
        engine.EndTurn(state, town);
        engine.EndTurn(state, town);
        var report = engine.EndTurn(state, town);
        check(report.WorkingResidents == 8 && report.UnemployedResidents == 0 &&
              report.OperatingCosts == 7,
            "Eight workers allocated deterministically with seven coins upkeep");
        check(report.GrossProduction[Resource.Food] == 14 &&
              report.GrossProduction[Resource.Wood] == 2,
            "Partial staffing scales lumber output while prioritizing earlier buildings");
        check(town.SetWorkPriority(2, 10) &&
              !town.SetWorkPriority(7, 10) &&
              !town.SetWorkPriority(0, 999),
            "Worker priority can be configured safely");
        var prioritized = engine.EndTurn(state, town);
        check(prioritized.GrossProduction[Resource.Wood] == 6 &&
              prioritized.GrossProduction[Resource.Food] == 9,
            "Prioritized camp receives full staffing at the expense of farm output");

        var broke = new GameState(wood: 0, stone: 0, food: 0, coins: 0);
        var starving = new Settlement(population: 1, economyMode: EconomyMode.Strategic);
        starving.SetTaxPolicy(TaxPolicy.Low); // one person pays zero coins
        var custom = new BuildingDefinition("test-mill", "Costly Machine",
            new Dictionary<Resource, int>(), 1,
            new Dictionary<Resource, int> { [Resource.Wood] = 10 },
            WorkersRequired: 1, UpkeepCoinsPerSeason: 5);
        check(starving.TryStartConstruction(broke, custom),
            "Custom production building can enter the settlement");
        engine.EndTurn(broke, starving);
        var noUpkeep = engine.EndTurn(broke, starving);
        check(noUpkeep.OperatingCosts == 0 && noUpkeep.WorkingResidents == 0 &&
              !noUpkeep.GrossProduction.ContainsKey(Resource.Wood),
            "A building unable to cover upkeep does not produce or employ workers");

        var spare = new Settlement(population: 10, economyMode: EconomyMode.Strategic);
        var spareTurn = engine.EndTurn(new GameState(food: 100), spare);
        check(spareTurn.UnemployedResidents == 10 &&
              spareTurn.WorkingResidents == 0,
            "Strategic workforce counts idle residents");
    }

    private static void TestProductionChains(Action<bool, string> check)
    {
        var map = World();
        var town = new Settlement(economyMode: EconomyMode.Classic);
        var state = new GameState(wood: 300, stone: 200, food: 100, coins: 300);
        check(town.TryStartConstruction(state, map,
                  new GridPosition(1, 2), BuildingCatalog.LumberCampId) &&
              town.TryStartConstruction(state, map,
                  new GridPosition(4, 2), BuildingCatalog.LumberMillId) &&
              town.TryStartConstruction(state, map,
                  new GridPosition(3, 4), BuildingCatalog.LumberMillId),
            "Two Lumber Mills and one forest-edge Lumber Camp can be built");
        var engine = new TurnEngine();
        engine.EndTurn(state, town, map);
        engine.EndTurn(state, town, map);
        var unconnected = engine.EndTurn(state, town, map);
        check(unconnected.GrossProduction[Resource.Wood] == 6,
            "Unconnected Lumber Mills do not boost production");

        RoadLine(map, 3, 1, 4);
        var linked = engine.EndTurn(state, town, map);
        check(linked.GrossProduction[Resource.Wood] == 9,
            "Road-connected Lumber Mill boosts six wood by fifty percent");
        check(map.Roads.PlaceDirt(new GridPosition(3, 5)).Succeeded &&
              map.Roads.PlaceDirt(new GridPosition(3, 4)).Failure ==
                  RoadFailure.BuildingOccupied &&
              linked.GrossProduction[Resource.Wood] == 9,
            "Additional mills cannot stack their production bonuses");

        var foodMap = World();
        var foodVillage = new Settlement(economyMode: EconomyMode.Classic);
        var foodState = new GameState(wood: 300, stone: 200, food: 100, coins: 300);
        check(foodVillage.TryStartConstruction(foodState, foodMap,
                  new GridPosition(6, 4), BuildingCatalog.FarmId) &&
              foodVillage.TryStartConstruction(foodState, foodMap,
                  new GridPosition(9, 2), BuildingCatalog.FisheryId) &&
              foodVillage.TryStartConstruction(foodState, foodMap,
                  new GridPosition(6, 2), BuildingCatalog.FoodProcessorId),
            "Farm, shoreline Fishery and Food Processor can be placed");
        engine.EndTurn(foodState, foodVillage, foodMap);
        engine.EndTurn(foodState, foodVillage, foodMap);
        engine.EndTurn(foodState, foodVillage, foodMap);
        var beforeRoad = engine.EndTurn(foodState, foodVillage, foodMap);
        check(beforeRoad.GrossProduction[Resource.Food] == 14,
            "Processing plant without roads does not increase food production");
        RoadLine(foodMap, 3, 6, 9);
        var withRoad = engine.EndTurn(foodState, foodVillage, foodMap);
        check(withRoad.GrossProduction[Resource.Food] == 17,
            "Connected processor increases farm and fishery output by twenty-five percent (floored)");
    }

    private static void TestMarket(Action<bool, string> check)
    {
        var island = World();
        var village = new Settlement(population: 8, economyMode: EconomyMode.Strategic);
        var inventory = new GameState(wood: 100, stone: 100, food: 100, coins: 100);
        check(village.TryStartConstruction(inventory, island,
                  new GridPosition(2, 4), BuildingCatalog.CottageId) &&
              village.TryStartConstruction(inventory, island,
                  new GridPosition(4, 4), BuildingCatalog.MarketplaceId),
            "Town builds a cottage and a Marketplace");
        var before = inventory.Get(Resource.Coins);
        check(MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Buy, Resource.Wood, 2).Failure ==
                  TradeFailure.InvalidMarket &&
              inventory.Get(Resource.Coins) == before,
            "Unfinished Marketplace cannot trade");
        var engine = new TurnEngine();
        engine.EndTurn(inventory, village, island);
        engine.EndTurn(inventory, village, island);
        check(MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Sell, Resource.Stone, 2).Failure ==
                  TradeFailure.NotConnected,
            "Completed but disconnected Marketplace cannot trade");

        RoadLine(island, 5, 2, 4);
        var woodBefore = inventory.Get(Resource.Wood);
        var coinsBefore = inventory.Get(Resource.Coins);
        var bought = MarketplaceTrading.Execute(inventory, village, island, 1,
            TradeDirection.Buy, Resource.Wood, 2);
        check(bought.Succeeded && bought.TotalCoins == 10 &&
              inventory.Get(Resource.Wood) == woodBefore + 2 &&
              inventory.Get(Resource.Coins) == coinsBefore - 10,
            "Market buy transfers coins and goods at buy price");
        var sold = MarketplaceTrading.Execute(inventory, village, island, 1,
            TradeDirection.Sell, Resource.Wood, 2);
        check(sold.Succeeded && sold.TotalCoins == 6 &&
              inventory.Get(Resource.Wood) == woodBefore &&
              inventory.Get(Resource.Coins) == coinsBefore - 4,
            "Selling bought goods incurs spread rather than free arbitrage");
        check(MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Buy, Resource.Coins, 1).Failure ==
                  TradeFailure.InvalidResource &&
              MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Buy, Resource.Wood, 0).Failure ==
                  TradeFailure.InvalidQuantity,
            "Trading disallows invalid goods and quantities");

        var quota = MarketplaceTrading.Execute(inventory, village, island, 1,
            TradeDirection.Sell, Resource.Stone, 26);
        check(quota.Succeeded &&
              MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Sell, Resource.Stone, 1).Failure ==
                  TradeFailure.SeasonalLimit,
            "Each market limits total bought and sold units to thirty per season");
        engine.EndTurn(inventory, village, island);
        check(MarketplaceTrading.Execute(inventory, village, island, 1,
                  TradeDirection.Sell, Resource.Stone, 1).Succeeded,
            "Market trade quota resets after ending a season");

        var overflowIsland = World();
        var overflowVillage = new Settlement(population: 8);
        var rich = new GameState(wood: 100, stone: 100,
            food: 100, coins: int.MaxValue);
        check(overflowVillage.TryStartConstruction(rich, overflowIsland,
                  new GridPosition(2, 4), BuildingCatalog.CottageId) &&
              overflowVillage.TryStartConstruction(rich, overflowIsland,
                  new GridPosition(4, 4), BuildingCatalog.MarketplaceId),
            "Large inventory can construct a Marketplace");
        engine.EndTurn(rich, overflowVillage, overflowIsland);
        engine.EndTurn(rich, overflowVillage, overflowIsland);
        RoadLine(overflowIsland, 5, 2, 4);
        // After construction costs, top up to the overflow boundary.
        rich.Add(Resource.Coins, int.MaxValue - rich.Get(Resource.Coins));
        var stonesBefore = rich.Get(Resource.Stone);
        check(MarketplaceTrading.Execute(rich, overflowVillage, overflowIsland, 1,
                  TradeDirection.Sell, Resource.Stone, 1).Failure ==
                  TradeFailure.InventoryOverflow &&
              rich.Get(Resource.Stone) == stonesBefore,
            "Market prevents overflow without losing goods");
    }
}
