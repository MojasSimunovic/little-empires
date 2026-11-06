using LittleEmpires.Core;

// Usage: [seasons], growth [seasons], or happiness [seasons].
// The default economy and growth scenarios remain regression benchmarks.
var growthMode = args.Length > 0 &&
    string.Equals(args[0], "growth", StringComparison.OrdinalIgnoreCase);
var happinessMode = args.Length > 0 &&
    string.Equals(args[0], "happiness", StringComparison.OrdinalIgnoreCase);
var seasonsArgIndex = growthMode || happinessMode ? 1 : 0;
var seasons = 8;
if (args.Length > seasonsArgIndex + 1 ||
    (args.Length == seasonsArgIndex + 1 &&
     (!int.TryParse(args[seasonsArgIndex], out seasons) || seasons < 1 || seasons > 80)))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/LittleEmpires.Simulator -- [seasons: 1..80]");
    Console.Error.WriteLine(
        "   or: dotnet run --project src/LittleEmpires.Simulator -- growth [seasons: 1..80]");
    Console.Error.WriteLine(
        "   or: dotnet run --project src/LittleEmpires.Simulator -- happiness [seasons: 1..80]");
    return 1;
}

var initialFood = happinessMode ? 0 : growthMode ? 30 : 12;
var startingHappiness = happinessMode ? 75 : 70;
var state = new GameState(wood: 80, stone: 25, food: initialFood, coins: 100);
var settlement = new Settlement(population: 8, happiness: startingHappiness);

PlannedConstruction[] constructionPlan = growthMode || happinessMode
    ? new[]
    {
        new PlannedConstruction(0, BuildingCatalog.CottageId),
        new PlannedConstruction(0, BuildingCatalog.FisheryId),
        new PlannedConstruction(1, BuildingCatalog.FarmId)
    }
    : new[]
    {
        new PlannedConstruction(0, BuildingCatalog.FisheryId),
        new PlannedConstruction(1, BuildingCatalog.FarmId),
        new PlannedConstruction(3, BuildingCatalog.LumberCampId),
        new PlannedConstruction(4, BuildingCatalog.QuarryId)
    };

var reports = new ScenarioRunner().Run(
    state, settlement, seasons,
    constructionPlan.Where(order => order.TurnIndex < seasons));

Console.WriteLine(happinessMode
    ? "LITTLE EMPIRES | Happiness & Recovery Scenario"
    : growthMode
        ? "LITTLE EMPIRES | Housing & Immigration Scenario"
        : "LITTLE EMPIRES | Settlement Economy Simulator");
Console.WriteLine($"Population: 8/8 | Starting happiness: {startingHappiness} " +
    $"| Starting stockpile: wood 80, stone 25, food {initialFood}, coins 100");
Console.WriteLine(growthMode || happinessMode
    ? "Build plan: Cottage + Fishery (turn 1), Farm (turn 2)"
    : "Build plan: Fishery (turn 1), Farm (turn 2), Lumber Camp (turn 4), Quarry (turn 5)");
Console.WriteLine();

foreach (var report in reports)
{
    var turn = report.Turn;
    var stock = report.EndingResources;
    Console.WriteLine($"Turn {report.TurnIndex + 1,2} | Year {turn.StartingYear} {turn.StartingSeason,-6} " +
        $"| Pop {turn.Population,2}/{turn.HousingCapacity,2} (+{turn.NewResidents}) " +
        $"| Happy {turn.Happiness,3} ({turn.HappinessChange,3:+0;-0;0}) " +
        $"| Wood {stock[Resource.Wood],3} | Stone {stock[Resource.Stone],3} " +
        $"| Food {stock[Resource.Food],3} | Coins {stock[Resource.Coins],3} " +
        $"| Food change {turn.ResourceChanges[Resource.Food],3:+0;-0;0} " +
        $"| Shortage {turn.FoodShortage}");

    foreach (var attempt in report.Construction)
        Console.WriteLine($"    Construction: {attempt.BuildingId} - {(attempt.Started ? "started" : "unaffordable")}");
    foreach (var name in turn.CompletedBuildings)
        Console.WriteLine($"    Completed: {name}");
}

Console.WriteLine();
Console.WriteLine($"Total immigrants: {reports.Sum(report => report.Turn.NewResidents)}");
Console.WriteLine($"Total food shortage: {reports.Sum(report => report.Turn.FoodShortage)}");
Console.WriteLine($"Final happiness: {settlement.Happiness}");
Console.WriteLine($"Final date: Year {state.Year} {state.Season}");
return 0;
