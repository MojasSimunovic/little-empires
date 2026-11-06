using LittleEmpires.Core;

/// <summary>
/// Scripted demonstration of the combined C# geography, roads, industry, tax,
/// trading and labor systems. No Unity or external dependencies required.
/// </summary>
internal static class StrategicScenario
{
    public static int Run(string[] args)
    {
        var seasons = 8;
        if (args.Length > 1 ||
            (args.Length == 1 &&
             (!int.TryParse(args[0], out seasons) || seasons < 1 || seasons > 80)))
        {
            Console.Error.WriteLine(
                "Usage: dotnet run --project src/LittleEmpires.Simulator -- strategic [seasons: 1..80]");
            return 1;
        }

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

        var island = new IslandMap(13, 9, tiles);
        var session = new WorldGameSession(
            new GameState(wood: 600, stone: 300, food: 250, coins: 500),
            new Settlement(population: 20, economyMode: EconomyMode.Strategic),
            island);
        session.ChangeTaxes(TaxPolicy.High);

        for (var x = 1; x <= 10; x++)
            Ensure(session.PlaceDirtRoad(new GridPosition(x, 3)).Succeeded, "Industry road");
        for (var x = 2; x <= 4; x++)
            Ensure(session.PlaceDirtRoad(new GridPosition(x, 5)).Succeeded, "Marketplace road");

        var buildings = new (string Id, GridPosition Position)[]
        {
            (BuildingCatalog.CottageId, new GridPosition(2, 4)),
            (BuildingCatalog.MarketplaceId, new GridPosition(4, 4)),
            (BuildingCatalog.LumberCampId, new GridPosition(1, 2)),
            (BuildingCatalog.LumberMillId, new GridPosition(4, 2)),
            (BuildingCatalog.FisheryId, new GridPosition(9, 2)),
            (BuildingCatalog.FarmId, new GridPosition(6, 4)),
            (BuildingCatalog.FoodProcessorId, new GridPosition(6, 2)),
            (BuildingCatalog.QuarryId, new GridPosition(10, 2))
        };
        foreach (var (id, position) in buildings)
            Ensure(session.PlaceBuilding(position, id), $"Could not start {id}");

        Console.WriteLine("LITTLE EMPIRES | Strategic Economy Integration");
        Console.WriteLine("Terrain, roads, +50% wood mill, +25% food processing, taxes, wages, marketplace");
        Console.WriteLine("Starting population 20 | Tax policy: High | Turn count: " + seasons);
        Console.WriteLine();

        for (var i = 0; i < seasons; i++)
        {
            var turn = session.EndSeason();
            var inventory = session.State;
            Console.WriteLine(
                $"Turn {i + 1,2} {turn.StartingSeason,-6} Year {turn.StartingYear} " +
                $"| Pop {turn.Population,2} Happy {turn.Happiness,3} " +
                $"| Wood {inventory.Get(Resource.Wood),3} " +
                $"Stone {inventory.Get(Resource.Stone),3} " +
                $"Food {inventory.Get(Resource.Food),3} " +
                $"Coins {inventory.Get(Resource.Coins),3} " +
                $"| Tax +{turn.TaxIncome} / Upkeep -{turn.OperatingCosts} " +
                $"| Staffed {turn.WorkingResidents}/{turn.StartingPopulation} " +
                $"| Output W:{turn.GrossProduction.GetValueOrDefault(Resource.Wood)} " +
                $"S:{turn.GrossProduction.GetValueOrDefault(Resource.Stone)} " +
                $"F:{turn.GrossProduction.GetValueOrDefault(Resource.Food)}");

            if (i == 2 && seasons > 3)
            {
                // Trading takes place between turns once the two-season
                // Marketplace is complete and connected to housing by roads.
                var sale = session.Trade(1, TradeDirection.Sell, Resource.Wood, 10);
                var purchase = session.Trade(1, TradeDirection.Buy, Resource.Stone, 3);
                Ensure(sale.Succeeded && purchase.Succeeded, "Marketplace trading");
                Console.WriteLine($"  MARKET: sold 10 wood for {sale.TotalCoins} coins; " +
                    $"bought 3 stone for {purchase.TotalCoins} coins");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Final state: " + session.State.Season + ", Year " + session.State.Year +
            ", residents " + session.Settlement.Population +
            ", happiness " + session.Settlement.Happiness);
        return 0;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
