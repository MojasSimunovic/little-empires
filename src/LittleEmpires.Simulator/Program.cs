using LittleEmpires.Core;

var seasons = 8;
if (args.Length > 1 ||
    (args.Length == 1 && (!int.TryParse(args[0], out seasons) || seasons < 1 || seasons > 80)))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/LittleEmpires.Simulator -- [seasons: 1..80]");
    return 1;
}

var state = new GameState(wood: 80, stone: 25, food: 12, coins: 100);
var settlement = new Settlement(population: 8);
var constructionPlan = new[]
{
    new PlannedConstruction(0, BuildingCatalog.FisheryId),
    new PlannedConstruction(1, BuildingCatalog.FarmId),
    new PlannedConstruction(3, BuildingCatalog.LumberCampId),
    new PlannedConstruction(4, BuildingCatalog.QuarryId)
};

var reports = new ScenarioRunner().Run(
    state, settlement, seasons,
    constructionPlan.Where(order => order.TurnIndex < seasons));

Console.WriteLine("LITTLE EMPIRES | Settlement Economy Simulator");
Console.WriteLine("Population: 8 | Starting stockpile: wood 80, stone 25, food 12, coins 100");
Console.WriteLine("Build plan: Fishery (turn 1), Farm (turn 2), Lumber Camp (turn 4), Quarry (turn 5)");
Console.WriteLine();

foreach (var report in reports)
{
    var turn = report.Turn;
    var stock = report.EndingResources;
    Console.WriteLine($"Turn {report.TurnIndex + 1,2} | Year {turn.StartingYear} {turn.StartingSeason,-6} " +
        $"| Wood {stock[Resource.Wood],3} | Stone {stock[Resource.Stone],3} " +
        $"| Food {stock[Resource.Food],3} | Coins {stock[Resource.Coins],3} " +
        $"| Food change {turn.ResourceChanges[Resource.Food],+3;-3; 0} " +
        $"| Shortage {turn.FoodShortage}");

    foreach (var attempt in report.Construction)
        Console.WriteLine($"    Construction: {attempt.BuildingId} - {(attempt.Started ? "started" : "unaffordable")}");
    foreach (var name in turn.CompletedBuildings)
        Console.WriteLine($"    Completed: {name}");
}

Console.WriteLine();
Console.WriteLine($"Total food shortage: {reports.Sum(report => report.Turn.FoodShortage)}");
Console.WriteLine($"Final date: Year {state.Year} {state.Season}");
return 0;
