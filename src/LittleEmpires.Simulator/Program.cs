using LittleEmpires.Core;

if (args.Length > 0 && string.Equals(args[0], "strategic", StringComparison.OrdinalIgnoreCase))
    return StrategicScenario.Run(args.Skip(1).ToArray());


// Usage: [seasons], growth [seasons], happiness [seasons], upgrade [seasons].
var growthMode = args.Length > 0 &&
    string.Equals(args[0], "growth", StringComparison.OrdinalIgnoreCase);
var happinessMode = args.Length > 0 &&
    string.Equals(args[0], "happiness", StringComparison.OrdinalIgnoreCase);
var upgradeMode = args.Length > 0 &&
    string.Equals(args[0], "upgrade", StringComparison.OrdinalIgnoreCase);
var seasonsArgIndex = growthMode || happinessMode || upgradeMode ? 1 : 0;
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
    Console.Error.WriteLine(
        "   or: dotnet run --project src/LittleEmpires.Simulator -- upgrade [seasons: 1..80]");
    Console.Error.WriteLine(
        "   or: dotnet run --project src/LittleEmpires.Simulator -- strategic [seasons: 1..80]");
    return 1;
}

var initialWood = upgradeMode ? 100 : 80;
var initialStone = upgradeMode ? 40 : 25;
var initialFood = happinessMode ? 0 : growthMode ? 30 : upgradeMode ? 60 : 12;
var startingHappiness = happinessMode ? 75 : 70;
var state = new GameState(wood: initialWood, stone: initialStone,
    food: initialFood, coins: 100);
var settlement = new Settlement(population: 8, happiness: startingHappiness);

PlannedConstruction[] constructionPlan = upgradeMode
    ? new[]
    {
        new PlannedConstruction(0, BuildingCatalog.CottageId),
        new PlannedConstruction(0, BuildingCatalog.FisheryId),
        new PlannedConstruction(1, BuildingCatalog.FarmId)
    }
    : growthMode || happinessMode
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

// The Cottage is the first building in the scripted settlement (index 0).
PlannedUpgrade[] upgradePlan = upgradeMode
    ? new[] { new PlannedUpgrade(3, 0) }
    : Array.Empty<PlannedUpgrade>();

var reports = new ScenarioRunner().Run(
    state, settlement, seasons,
    constructionPlan.Where(order => order.TurnIndex < seasons),
    upgradePlan.Where(order => order.TurnIndex < seasons));

Console.WriteLine(upgradeMode
    ? "LITTLE EMPIRES | Cottage to Townhouse Upgrade Scenario"
    : happinessMode
        ? "LITTLE EMPIRES | Happiness & Recovery Scenario"
        : growthMode
            ? "LITTLE EMPIRES | Housing & Immigration Scenario"
            : "LITTLE EMPIRES | Settlement Economy Simulator");
Console.WriteLine($"Population: 8/8 | Starting happiness: {startingHappiness} " +
    $"| Starting stockpile: wood {initialWood}, stone {initialStone}, food {initialFood}, coins 100");
Console.WriteLine(upgradeMode
    ? "Build: Cottage + Fishery (turn 1), Farm (turn 2); upgrade Cottage (turn 4)"
    : growthMode || happinessMode
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
    foreach (var attempt in report.Upgrades)
        Console.WriteLine($"    Upgrade: building #{attempt.BuildingIndex} - {(attempt.Started ? "started" : "rejected")}");
    foreach (var name in turn.CompletedBuildings)
        Console.WriteLine($"    Completed: {name}");
    foreach (var name in turn.CompletedUpgrades)
        Console.WriteLine($"    Upgraded: {name}");
}

Console.WriteLine();
Console.WriteLine($"Total immigrants: {reports.Sum(report => report.Turn.NewResidents)}");
Console.WriteLine($"Total food shortage: {reports.Sum(report => report.Turn.FoodShortage)}");
Console.WriteLine($"Final happiness: {settlement.Happiness}");
Console.WriteLine($"Final date: Year {state.Year} {state.Season}");
return 0;
