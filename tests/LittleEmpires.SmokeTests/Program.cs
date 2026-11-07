using LittleEmpires.Core;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var state = new GameState();
var settlement = new Settlement();
var engine = new TurnEngine();
var lumberCamp = new BuildingDefinition(
    "lumber-camp", "Lumber Camp",
    new Dictionary<Resource, int> { [Resource.Wood] = 20 },
    2,
    new Dictionary<Resource, int> { [Resource.Wood] = 5 });

Check(settlement.TryStartConstruction(state, lumberCamp), "Can build when affordable");
Check(state.Get(Resource.Wood) == 30, "Construction cost paid upfront");
Check(!settlement.Buildings[0].IsComplete, "Building starts under construction");

var springSummary = engine.EndTurn(state, settlement);
Check(springSummary.StartingSeason == Season.Spring && springSummary.EndingSeason == Season.Summer,
    "Turn summary records starting and ending seasons");
Check(springSummary.ResourceChanges[Resource.Wood] == 0, "No production during construction in summary");
Check(springSummary.CompletedBuildings.Count == 0, "No premature building completion");
Check(state.Season == Season.Summer, "Season advances");
Check(state.Get(Resource.Wood) == 30, "No production during construction");

var summerSummary = engine.EndTurn(state, settlement);
Check(summerSummary.CompletedBuildings.SequenceEqual(new[] { "Lumber Camp" }),
    "Turn summary reports completed building");
Check(settlement.Buildings[0].IsComplete, "Building completes after two seasons");
Check(state.Get(Resource.Wood) == 30, "No production on completion season");

var autumnSummary = engine.EndTurn(state, settlement);
Check(autumnSummary.ResourceChanges[Resource.Wood] == 5, "Turn summary reports production delta");
Check(autumnSummary.CompletedBuildings.Count == 0, "Completed buildings not reported twice");
Check(state.Get(Resource.Wood) == 35, "Completed building produces next season");

var winterSummary = engine.EndTurn(state, settlement);
Check(winterSummary.StartingYear == 1 && winterSummary.EndingYear == 2,
    "Turn summary records year rollover");
Check(state.Season == Season.Spring && state.Year == 2, "Winter advances to next year");

var expensive = lumberCamp with { Cost = new Dictionary<Resource, int> { [Resource.Wood] = 1000 } };
Check(!settlement.TryStartConstruction(state, expensive), "Cannot build without resources");
Check(settlement.Buildings.Count == 1, "Failed construction does not create a building");

var hungryState = new GameState(food: 6);
var village = new Settlement(population: 4);
var firstMeal = engine.EndTurn(hungryState, village);
Check(firstMeal.FoodRequired == 4 && firstMeal.FoodConsumed == 4 && firstMeal.FoodShortage == 0,
    "Population consumes one food each when sufficient");
Check(hungryState.Get(Resource.Food) == 2 && firstMeal.ResourceChanges[Resource.Food] == -4,
    "Food inventory and resource delta reflect consumption");
var secondMeal = engine.EndTurn(hungryState, village);
Check(secondMeal.FoodConsumed == 2 && secondMeal.FoodShortage == 2,
    "Shortage reported when food is insufficient");
Check(hungryState.Get(Resource.Food) == 0 && village.Population == 4,
    "Food cannot become negative and shortage does not kill population");

var fishingState = new GameState(food: 0);
var fishingVillage = new Settlement(population: 3);
var fishery = new BuildingDefinition("fishery", "Fishery",
    new Dictionary<Resource, int>(), 1,
    new Dictionary<Resource, int> { [Resource.Food] = 5 });
Check(fishingVillage.TryStartConstruction(fishingState, fishery), "Fishery construction started");
var constructionMeal = engine.EndTurn(fishingState, fishingVillage);
Check(constructionMeal.FoodShortage == 3 && constructionMeal.CompletedBuildings.Contains("Fishery"),
    "Newly completed fishery cannot feed population in completion season");
var fishMeal = engine.EndTurn(fishingState, fishingVillage);
Check(fishMeal.FoodShortage == 0 && fishMeal.FoodConsumed == 3 &&
      fishMeal.ResourceChanges[Resource.Food] == 2 && fishingState.Get(Resource.Food) == 2,
    "Existing fishery produces before consumption");

var emptyVillage = new Settlement();
var emptyTurn = engine.EndTurn(new GameState(food: 0), emptyVillage);
Check(emptyTurn.FoodRequired == 0 && emptyTurn.FoodShortage == 0,
    "Zero population consumes no food");
try
{
    emptyVillage.SetPopulation(-1);
    throw new Exception("Negative population was accepted");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("PASS: Negative population rejected");
}


var definitions = BuildingCatalog.All;
Check(definitions.Count == 9, "Catalogue contains four producers, housing, processors and Marketplace");
Check(definitions.Select(building => building.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 9,
    "Every catalogue building has a unique ID");
foreach (var definition in definitions)
{
    definition.Validate();
    Check((definition.ProductionPerSeason.Count > 0 || definition.HousingCapacity > 0 ||
          definition.Id is BuildingCatalog.LumberMillId or
              BuildingCatalog.FoodProcessorId or BuildingCatalog.MarketplaceId) &&
          definition.ConstructionSeasons >= 1, $"{definition.Name} definition is valid");
}
Check(BuildingCatalog.TryGet("FARM", out var selectedFarm) &&
      selectedFarm?.Id == BuildingCatalog.FarmId,
    "Catalogue supports case-insensitive lookup");
Check(!BuildingCatalog.TryGet("unknown", out var missingBuilding) &&
      missingBuilding is null,
    "Unknown catalogue ID is not found");

try
{
    BuildingCatalog.Get("unknown");
    throw new Exception("Unknown building ID was accepted");
}
catch (KeyNotFoundException)
{
    Console.WriteLine("PASS: Unknown building ID rejected");
}

var catalogueState = new GameState(wood: 200, stone: 100, food: 0, coins: 100);
var catalogueSettlement = new Settlement();
foreach (var definition in definitions.Where(building => building.ProductionPerSeason.Count > 0))
    Check(catalogueSettlement.TryStartConstruction(catalogueState, definition.Id),
        $"{definition.Name} can be built by catalogue ID");
Check(catalogueState.Get(Resource.Wood) == 128 &&
      catalogueState.Get(Resource.Stone) == 92 &&
      catalogueState.Get(Resource.Coins) == 55,
    "All catalogue building costs are paid upfront");

var catalogueSpring = engine.EndTurn(catalogueState, catalogueSettlement);
Check(catalogueSpring.CompletedBuildings.SequenceEqual(new[] { "Fishery" }),
    "One-season Fishery finishes first");
Check(catalogueSpring.ResourceChanges[Resource.Food] == 0,
    "Newly finished Fishery does not produce immediately");

var catalogueSummer = engine.EndTurn(catalogueState, catalogueSettlement);
Check(catalogueSummer.ResourceChanges[Resource.Food] == 5,
    "Fishery begins producing the season after completion");
Check(catalogueSummer.CompletedBuildings.Count == 3 &&
      catalogueSummer.CompletedBuildings.Contains("Farm") &&
      catalogueSummer.CompletedBuildings.Contains("Lumber Camp") &&
      catalogueSummer.CompletedBuildings.Contains("Quarry"),
    "Two-season buildings finish together");

var catalogueAutumn = engine.EndTurn(catalogueState, catalogueSettlement);
Check(catalogueAutumn.ResourceChanges[Resource.Wood] == 6 &&
      catalogueAutumn.ResourceChanges[Resource.Stone] == 4 &&
      catalogueAutumn.ResourceChanges[Resource.Food] == 14,
    "Completed catalogue buildings generate their configured production");

var brokeState = new GameState(wood: 0, stone: 0, food: 0, coins: 0);
var brokeSettlement = new Settlement();
Check(!brokeSettlement.TryStartConstruction(brokeState, BuildingCatalog.FisheryId) &&
      brokeSettlement.Buildings.Count == 0,
    "Catalogue construction rejects unaffordable buildings without side effects");


var scenarioState = new GameState(wood: 80, stone: 25, food: 12, coins: 100);
var scenarioSettlement = new Settlement(population: 8);
var scenarioPlan = new[]
{
    new PlannedConstruction(0, BuildingCatalog.FisheryId),
    new PlannedConstruction(1, BuildingCatalog.FarmId),
    new PlannedConstruction(3, BuildingCatalog.LumberCampId),
    new PlannedConstruction(4, BuildingCatalog.QuarryId)
};
var scenarioReports = new ScenarioRunner().Run(scenarioState, scenarioSettlement, 8, scenarioPlan);
Check(scenarioReports.Count == 8, "Scenario runner generates one report per turn");
Check(scenarioReports.All(report => report.Construction.All(attempt => attempt.Started)),
    "All scripted construction orders are affordable");
Check(scenarioReports[0].Construction.Single().BuildingId == BuildingCatalog.FisheryId &&
      scenarioReports[0].Turn.CompletedBuildings.Contains("Fishery"),
    "Scripted Fishery starts and finishes on first turn");
Check(scenarioReports[2].Turn.FoodShortage == 2 &&
      scenarioReports[2].EndingResources[Resource.Food] == 0,
    "Scripted scenario experiences temporary food shortage");
Check(scenarioReports[3].Turn.FoodShortage == 0 &&
      scenarioReports[3].Turn.ResourceChanges[Resource.Food] == 6,
    "Farm production allows food supply to recover");
Check(scenarioReports.Sum(report => report.Turn.FoodShortage) == 2,
    "Total food shortage is deterministic");
Check(scenarioState.Get(Resource.Wood) == 26 &&
      scenarioState.Get(Resource.Stone) == 25 &&
      scenarioState.Get(Resource.Food) == 30 &&
      scenarioState.Get(Resource.Coins) == 55,
    "Two-year scenario ends with expected resource balances");
Check(scenarioState.Year == 3 && scenarioState.Season == Season.Spring,
    "Eight seasonal turns advance exactly two years");
Check(scenarioReports[0].EndingResources[Resource.Wood] == 68 &&
      scenarioReports[7].EndingResources[Resource.Wood] == 26,
    "Season reports preserve historical stockpile snapshots");

var unaffordableScenario = new ScenarioRunner().Run(
    new GameState(wood: 0, stone: 0, food: 0, coins: 0),
    new Settlement(), 1,
    new[] { new PlannedConstruction(0, BuildingCatalog.FisheryId) });
Check(unaffordableScenario[0].Construction.Count == 1 &&
      !unaffordableScenario[0].Construction[0].Started,
    "Scenario runner reports failed construction attempts");


var cottage = BuildingCatalog.Get(BuildingCatalog.CottageId);
Check(cottage.HousingCapacity == 6 && cottage.ProductionPerSeason.Count == 0 &&
      cottage.ConstructionSeasons == 1,
    "Cottage provides six housing slots and no resources");
Check(cottage.Cost[Resource.Wood] == 18 &&
      cottage.Cost[Resource.Stone] == 5 &&
      cottage.Cost[Resource.Coins] == 10,
    "Cottage has the expected construction costs");

var housingState = new GameState(food: 20);
var housingVillage = new Settlement(population: 8);
Check(housingVillage.HousingCapacity == 8 && housingVillage.AvailableHousing == 0,
    "Founding residents have housing but no spare capacity");
Check(housingVillage.TryStartConstruction(housingState, BuildingCatalog.CottageId),
    "Cottage construction starts");
Check(housingVillage.HousingCapacity == 8,
    "Unfinished cottage provides no housing capacity");
var firstImmigration = engine.EndTurn(housingState, housingVillage);
Check(firstImmigration.CompletedBuildings.Contains("Cottage") &&
      firstImmigration.HousingCapacity == 14,
    "Completed cottage immediately increases housing capacity");
Check(firstImmigration.StartingPopulation == 8 &&
      firstImmigration.Population == 10 &&
      firstImmigration.NewResidents == 2,
    "Two residents immigrate after housing completes");
Check(firstImmigration.FoodRequired == 8 &&
      firstImmigration.FoodConsumed == 8 &&
      housingState.Get(Resource.Food) == 12,
    "New immigrants do not consume food on arrival turn");

var secondImmigration = engine.EndTurn(housingState, housingVillage);
Check(secondImmigration.FoodRequired == 10 &&
      secondImmigration.NewResidents == 2 &&
      secondImmigration.Population == 12,
    "New arrivals consume food starting next season");
var shortageImmigration = engine.EndTurn(housingState, housingVillage);
Check(shortageImmigration.FoodShortage == 10 &&
      shortageImmigration.NewResidents == 0 &&
      housingVillage.Population == 12,
    "Food shortage blocks immigration without reducing population");

var foodLimitedState = new GameState(food: 9);
var foodLimitedVillage = new Settlement(population: 8);
Check(foodLimitedVillage.TryStartConstruction(foodLimitedState, BuildingCatalog.CottageId),
    "Food-limited settlement starts a cottage");
var foodLimitedTurn = engine.EndTurn(foodLimitedState, foodLimitedVillage);
Check(foodLimitedTurn.NewResidents == 1 &&
      foodLimitedTurn.Population == 9 &&
      foodLimitedState.Get(Resource.Food) == 1,
    "Only one resident immigrates with one food of surplus");

var cappedState = new GameState(food: 100);
var cappedVillage = new Settlement(population: 8);
Check(cappedVillage.TryStartConstruction(cappedState, BuildingCatalog.CottageId),
    "Housing-capacity settlement starts a cottage");
var cappedTurns = Enumerable.Range(0, 4)
    .Select(_ => engine.EndTurn(cappedState, cappedVillage)).ToArray();
Check(cappedTurns.Select(turn => turn.NewResidents).SequenceEqual(new[] { 2, 2, 2, 0 }) &&
      cappedVillage.Population == 14 && cappedVillage.HousingCapacity == 14,
    "Immigration stops at housing capacity and never exceeds two per season");

var noHousingState = new GameState(food: 100);
var noHousingVillage = new Settlement(population: 8);
Check(engine.EndTurn(noHousingState, noHousingVillage).NewResidents == 0,
    "Food surplus alone cannot grow population without housing");
try
{
    noHousingVillage.SetPopulation(9);
    throw new Exception("Population was allowed to exceed housing capacity");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("PASS: Population cannot exceed housing capacity");
}

var growthState = new GameState(wood: 80, stone: 25, food: 30, coins: 100);
var growthVillage = new Settlement(population: 8);
var growthPlan = new[]
{
    new PlannedConstruction(0, BuildingCatalog.CottageId),
    new PlannedConstruction(0, BuildingCatalog.FisheryId),
    new PlannedConstruction(1, BuildingCatalog.FarmId)
};
var growthReports = new ScenarioRunner().Run(growthState, growthVillage, 8, growthPlan);
Check(growthReports.All(report => report.Construction.All(attempt => attempt.Started)),
    "Growth scenario can afford its planned buildings");
Check(growthReports.Select(report => report.Turn.Population)
      .SequenceEqual(new[] { 10, 12, 14, 14, 14, 14, 14, 14 }),
    "Growth scenario reaches housing capacity after three seasons");
Check(growthReports.Sum(report => report.Turn.NewResidents) == 6 &&
      growthReports.All(report => report.Turn.FoodShortage == 0),
    "Growth scenario attracts six residents without shortages");
Check(growthState.Get(Resource.Wood) == 25 &&
      growthState.Get(Resource.Stone) == 12 &&
      growthState.Get(Resource.Food) == 10 &&
      growthState.Get(Resource.Coins) == 70,
    "Growth scenario ends with expected stockpile after eight seasons");


Check(new Settlement(population: 2).Happiness == 70,
    "Settlements start with neutral-positive happiness");
try
{
    _ = new Settlement(population: 1, happiness: 101);
    throw new Exception("Happiness above 100 was accepted");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("PASS: Happiness above 100 rejected");
}
try
{
    _ = new Settlement(population: 1, happiness: -1);
    throw new Exception("Negative happiness was accepted");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("PASS: Negative happiness rejected");
}

var happyState = new GameState(food: 20);
var happyVillage = new Settlement(population: 8);
Check(happyVillage.TryStartConstruction(happyState, BuildingCatalog.CottageId),
    "Happy settlement can start a cottage");
var happyTurn = engine.EndTurn(happyState, happyVillage);
Check(happyTurn.StartingHappiness == 70 &&
      happyTurn.Happiness == 73 &&
      happyTurn.HappinessChange == 3,
    "Fully fed settlement with spare housing gains three happiness");
Check(happyTurn.NewResidents == 2,
    "Happy settlement attracts two immigrants");

var unhappyState = new GameState(food: 0);
var unhappyVillage = new Settlement(population: 8, happiness: 60);
Check(unhappyVillage.TryStartConstruction(unhappyState, BuildingCatalog.CottageId),
    "Unhappy settlement can start a cottage");
var unhappyTurn = engine.EndTurn(unhappyState, unhappyVillage);
Check(unhappyTurn.FoodShortage == 8 &&
      unhappyTurn.Happiness == 49 &&
      unhappyTurn.HappinessChange == -11 &&
      unhappyTurn.NewResidents == 0,
    "Food shortage reduces happiness and blocks immigration");

var bottomVillage = new Settlement(population: 1, happiness: 0);
var bottomTurn = engine.EndTurn(new GameState(food: 0), bottomVillage);
Check(bottomTurn.Happiness == 0 && bottomTurn.HappinessChange == 0,
    "Happiness never drops below zero");
var topVillage = new Settlement(population: 1, happiness: 100);
var topTurn = engine.EndTurn(new GameState(food: 100), topVillage);
Check(topTurn.Happiness == 100 && topTurn.HappinessChange == 0,
    "Happiness never exceeds one hundred");

static TurnSummary ImmigrationAtHappiness(int initialHappiness)
{
    var state = new GameState(food: 30);
    var village = new Settlement(population: 8, happiness: initialHappiness);
    if (!village.TryStartConstruction(state, BuildingCatalog.CottageId))
        throw new Exception("Failed to construct cottage for happiness threshold test");
    return new TurnEngine().EndTurn(state, village);
}

var lowHappinessTurn = ImmigrationAtHappiness(46);
var moderateHappinessTurn = ImmigrationAtHappiness(47);
var highHappinessTurn = ImmigrationAtHappiness(67);
Check(lowHappinessTurn.Happiness == 49 && lowHappinessTurn.NewResidents == 0,
    "Below 50 happiness prevents immigration despite food and housing");
Check(moderateHappinessTurn.Happiness == 50 && moderateHappinessTurn.NewResidents == 1,
    "At 50 happiness, one immigrant can arrive");
Check(highHappinessTurn.Happiness == 70 && highHappinessTurn.NewResidents == 2,
    "At 70 happiness, two immigrants can arrive");

var happinessScenarioState = new GameState(wood: 80, stone: 25, food: 0, coins: 100);
var happinessScenarioVillage = new Settlement(population: 8, happiness: 75);
var happinessScenarioReports = new ScenarioRunner().Run(
    happinessScenarioState, happinessScenarioVillage, 8, growthPlan);
Check(happinessScenarioReports.Select(report => report.Turn.Happiness)
      .SequenceEqual(new[] { 64, 53, 42, 45, 48, 51, 54, 57 }),
    "Happiness scenario falls during shortages and recovers gradually");
Check(happinessScenarioReports.Select(report => report.Turn.NewResidents)
      .SequenceEqual(new[] { 0, 0, 0, 0, 0, 1, 1, 1 }),
    "Happiness scenario suppresses immigration until happiness recovers");
Check(happinessScenarioReports.Sum(report => report.Turn.FoodShortage) == 14 &&
      happinessScenarioVillage.Population == 11 &&
      happinessScenarioState.Get(Resource.Food) == 27,
    "Happiness scenario recovers food and ends with eleven residents");
Check(growthReports.Select(report => report.Turn.Population)
      .SequenceEqual(new[] { 10, 12, 14, 14, 14, 14, 14, 14 }),
    "Existing growth scenario still reaches fourteen residents");


var townhouse = BuildingCatalog.Get(BuildingCatalog.TownhouseId);
Check(!townhouse.CanBuildDirectly && townhouse.HousingCapacity == 12,
    "Townhouse is an upgrade-only building with twelve housing slots");
var directTownhouseState = new GameState();
var directTownhouseVillage = new Settlement(population: 8);
Check(!directTownhouseVillage.TryStartConstruction(directTownhouseState, BuildingCatalog.TownhouseId) &&
      directTownhouseVillage.Buildings.Count == 0,
    "Townhouse cannot be built directly, even though its definition has no direct cost");

var upgradeDefinition = HousingUpgradeCatalog.CottageToTownhouse;
upgradeDefinition.Validate();
Check(upgradeDefinition.FromBuildingId == BuildingCatalog.CottageId &&
      upgradeDefinition.ToBuildingId == BuildingCatalog.TownhouseId &&
      upgradeDefinition.ConstructionSeasons == 2 &&
      upgradeDefinition.Cost[Resource.Wood] == 24 &&
      upgradeDefinition.Cost[Resource.Stone] == 10 &&
      upgradeDefinition.Cost[Resource.Coins] == 20,
    "Cottage-to-Townhouse upgrade has the correct cost and duration");

var upgradeState = new GameState(wood: 100, stone: 40, food: 100, coins: 100);
var upgradeVillage = new Settlement(population: 8);
Check(upgradeVillage.TryStartConstruction(upgradeState, BuildingCatalog.CottageId),
    "Cottage can be built before upgrading");
Check(!upgradeVillage.TryStartUpgrade(upgradeState, 0) &&
      upgradeState.Get(Resource.Wood) == 82,
    "Unfinished cottage cannot be upgraded or charged");
engine.EndTurn(upgradeState, upgradeVillage);
Check(upgradeVillage.HousingCapacity == 14 && upgradeVillage.Population == 10,
    "Completed cottage houses residents before upgrade begins");
Check(!upgradeVillage.TryStartUpgrade(upgradeState, 1) &&
      !upgradeVillage.TryStartUpgrade(upgradeState, -1),
    "Invalid building indices cannot be upgraded");
Check(upgradeVillage.TryStartUpgrade(upgradeState, 0),
    "Completed cottage can begin upgrade");
Check(upgradeState.Get(Resource.Wood) == 58 &&
      upgradeState.Get(Resource.Stone) == 25 &&
      upgradeState.Get(Resource.Coins) == 70,
    "Upgrade costs are paid upfront exactly once");
Check(upgradeVillage.Buildings[0].IsUpgrading &&
      upgradeVillage.Buildings[0].UpgradeSeasonsRemaining == 2 &&
      upgradeVillage.Buildings[0].PendingUpgradeToId == BuildingCatalog.TownhouseId &&
      upgradeVillage.HousingCapacity == 14,
    "Cottage retains six housing slots while upgrade is pending");
Check(!upgradeVillage.TryStartUpgrade(upgradeState, 0) &&
      upgradeState.Get(Resource.Coins) == 70,
    "A building cannot begin a second upgrade while already upgrading");

var upgradeSeasonOne = engine.EndTurn(upgradeState, upgradeVillage);
Check(upgradeSeasonOne.CompletedUpgrades.Count == 0 &&
      upgradeVillage.Buildings[0].UpgradeSeasonsRemaining == 1 &&
      upgradeSeasonOne.HousingCapacity == 14 &&
      upgradeSeasonOne.Population == 12,
    "First upgrade season preserves original housing and residents");
var upgradeSeasonTwo = engine.EndTurn(upgradeState, upgradeVillage);
Check(upgradeSeasonTwo.CompletedUpgrades.SequenceEqual(new[] { "Cottage → Townhouse" }) &&
      upgradeSeasonTwo.HousingCapacity == 20 &&
      upgradeSeasonTwo.Population == 14,
    "Second upgrade season unlocks six additional housing slots");
Check(upgradeVillage.Buildings.Count == 1 &&
      upgradeVillage.Buildings[0].Definition.Id == BuildingCatalog.TownhouseId &&
      !upgradeVillage.Buildings[0].IsUpgrading &&
      upgradeVillage.Buildings[0].UpgradeSeasonsRemaining == 0,
    "Upgrade transforms the same building instead of creating another");
Check(!upgradeVillage.TryStartUpgrade(upgradeState, 0),
    "Completed Townhouse cannot be upgraded a second time");

var unaffordableUpgradeState = new GameState(wood: 18, stone: 5, food: 100, coins: 10);
var unaffordableUpgradeVillage = new Settlement(population: 8);
Check(unaffordableUpgradeVillage.TryStartConstruction(unaffordableUpgradeState, BuildingCatalog.CottageId),
    "Minimum-resource settlement can build Cottage");
engine.EndTurn(unaffordableUpgradeState, unaffordableUpgradeVillage);
Check(!unaffordableUpgradeVillage.TryStartUpgrade(unaffordableUpgradeState, 0) &&
      unaffordableUpgradeVillage.Buildings[0].Definition.Id == BuildingCatalog.CottageId &&
      !unaffordableUpgradeVillage.Buildings[0].IsUpgrading,
    "Unaffordable upgrades leave the building and resources unchanged");

var nonHousingUpgradeState = new GameState(food: 100);
var nonHousingUpgradeVillage = new Settlement(population: 8);
Check(nonHousingUpgradeVillage.TryStartConstruction(nonHousingUpgradeState, BuildingCatalog.FisheryId),
    "Fishery can be built for upgrade eligibility test");
engine.EndTurn(nonHousingUpgradeState, nonHousingUpgradeVillage);
Check(!nonHousingUpgradeVillage.TryStartUpgrade(nonHousingUpgradeState, 0),
    "Non-housing buildings cannot use the Cottage upgrade");

var townhouseScenarioState = new GameState(wood: 100, stone: 40, food: 60, coins: 100);
var townhouseScenarioVillage = new Settlement(population: 8);
var townhousePlan = new[]
{
    new PlannedConstruction(0, BuildingCatalog.CottageId),
    new PlannedConstruction(0, BuildingCatalog.FisheryId),
    new PlannedConstruction(1, BuildingCatalog.FarmId)
};
var townhouseUpgradePlan = new[] { new PlannedUpgrade(3, 0) };
var townhouseReports = new ScenarioRunner().Run(
    townhouseScenarioState, townhouseScenarioVillage, 8,
    townhousePlan, townhouseUpgradePlan);
Check(townhouseReports[3].Upgrades.Count == 1 &&
      townhouseReports[3].Upgrades[0].Started &&
      townhouseReports[4].Turn.CompletedUpgrades.Contains("Cottage → Townhouse"),
    "Scripted upgrade starts in Winter and finishes in following Spring");
Check(townhouseReports.Select(report => report.Turn.HousingCapacity)
      .SequenceEqual(new[] { 14, 14, 14, 14, 20, 20, 20, 20 }),
    "Housing capacity expands only on upgrade completion");
Check(townhouseReports.Select(report => report.Turn.Population)
      .SequenceEqual(new[] { 10, 12, 14, 14, 16, 18, 20, 20 }),
    "Immigration resumes after housing upgrade and stops at new capacity");
Check(townhouseReports.Select(report => report.Turn.Happiness)
      .SequenceEqual(new[] { 73, 76, 79, 80, 83, 86, 89, 90 }),
    "Housing upgrade feeds into existing happiness calculations");
Check(townhouseReports.All(report => report.Turn.FoodShortage == 0) &&
      townhouseScenarioState.Get(Resource.Food) == 28 &&
      townhouseScenarioState.Get(Resource.Wood) == 21 &&
      townhouseScenarioState.Get(Resource.Stone) == 17 &&
      townhouseScenarioState.Get(Resource.Coins) == 50,
    "Two-year townhouse scenario ends with deterministic resource balances");
Check(townhouseScenarioVillage.Buildings.Count == 3 &&
      townhouseScenarioVillage.Buildings[0].Definition.Id == BuildingCatalog.TownhouseId,
    "Scripted upgrade does not duplicate the original Cottage");


var sitesToVerify = new[]
{
    BuildingCatalog.QuarryId,
    BuildingCatalog.LumberCampId,
    BuildingCatalog.FisheryId,
    BuildingCatalog.FarmId,
    BuildingCatalog.CottageId
};
foreach (var size in new[] { (Width: 9, Height: 9), (Width: 13, Height: 13),
                           (Width: 20, Height: 14) })
{
    foreach (var seed in Enumerable.Range(0, 40))
    {
        var firstMap = IslandGenerator.Generate(seed, size.Width, size.Height);
        var secondMap = IslandGenerator.Generate(seed, size.Width, size.Height);
        Check(firstMap.Width == size.Width && firstMap.Height == size.Height &&
              firstMap.Tiles.Count == size.Width * size.Height &&
              firstMap.Tiles.Select(t => t.Terrain)
                  .SequenceEqual(secondMap.Tiles.Select(t => t.Terrain)),
            $"Island is complete and deterministic for seed {seed}, size {size.Width}x{size.Height}");

        foreach (var buildingId in sitesToVerify)
        {
            var definition = BuildingCatalog.Get(buildingId);
            Check(firstMap.Tiles.Any(tile => BuildingPlacementRules.Evaluate(
                      firstMap, tile.Position, definition).Allowed),
                $"Seed {seed} has at least one valid site for {definition.Name}");
        }
        Check(firstMap.GetTile(new GridPosition(0, 0)).Terrain == TerrainKind.Water,
            "Island corners remain water");
    }
}

var terrainMap = IslandGenerator.Generate(17);
var quarryDefinition = BuildingCatalog.Get(BuildingCatalog.QuarryId);
var lumberDefinition = BuildingCatalog.Get(BuildingCatalog.LumberCampId);
var fisheryDefinition = BuildingCatalog.Get(BuildingCatalog.FisheryId);
var farmDefinition = BuildingCatalog.Get(BuildingCatalog.FarmId);
var cottageDefinition = BuildingCatalog.Get(BuildingCatalog.CottageId);
var mountainTile = terrainMap.Tiles.First(tile => tile.Terrain == TerrainKind.Mountain);
var forestTile = terrainMap.Tiles.First(tile => tile.Terrain == TerrainKind.Forest);
var grassTile = terrainMap.GetTile(new GridPosition(terrainMap.Width / 2, terrainMap.Height / 2));
var waterTile = terrainMap.GetTile(new GridPosition(0, 0));
Check(BuildingPlacementRules.Evaluate(terrainMap, mountainTile.Position, quarryDefinition).Allowed,
    "Quarry can be placed directly on a mountain");
Check(BuildingPlacementRules.Evaluate(terrainMap, grassTile.Position, quarryDefinition).Failure ==
      PlacementFailure.RequiresMountain,
    "Quarry is rejected on normal flat land");
Check(BuildingPlacementRules.Evaluate(terrainMap, forestTile.Position, cottageDefinition).Failure ==
      PlacementFailure.RequiresFlatLand,
    "Housing cannot clear a forest by being placed on it");
Check(BuildingPlacementRules.Evaluate(terrainMap, waterTile.Position, cottageDefinition).Failure ==
      PlacementFailure.Water,
    "Buildings cannot be placed on water");
Check(BuildingPlacementRules.Evaluate(terrainMap, new GridPosition(-1, 0), cottageDefinition).Failure ==
      PlacementFailure.OutsideMap,
    "Building placement rejects coordinates outside the island");
Check(BuildingPlacementRules.Evaluate(terrainMap, grassTile.Position,
      BuildingCatalog.Get(BuildingCatalog.TownhouseId)).Failure ==
      PlacementFailure.UpgradeOnly,
    "Townhouses cannot bypass the upgrade-only restriction");
Check(terrainMap.Tiles.Any(tile => BuildingPlacementRules.Evaluate(
          terrainMap, tile.Position, lumberDefinition).Allowed &&
          terrainMap.Neighbours(tile.Position).Any(t => t.Terrain == TerrainKind.Forest)),
    "Lumber Camp placement requires orthogonal forest adjacency");
Check(terrainMap.Tiles.Any(tile => BuildingPlacementRules.Evaluate(
          terrainMap, tile.Position, fisheryDefinition).Allowed &&
          terrainMap.IsShoreline(tile.Position)),
    "Fishery placement requires a shoreline plot");
Check(terrainMap.Tiles.Any(tile => BuildingPlacementRules.Evaluate(
          terrainMap, tile.Position, farmDefinition).Allowed &&
          tile.Terrain == TerrainKind.FertileLand),
    "Farm placement requires fertile terrain");

var worldState = new GameState(wood: 100, stone: 25, food: 100, coins: 100);
var worldSettlement = new Settlement(population: 8);
Check(worldSettlement.TryStartConstruction(
      worldState, terrainMap, mountainTile.Position, BuildingCatalog.QuarryId),
    "Spatial construction accepts Quarry on mountain");
Check(worldSettlement.Buildings[0].Position == mountainTile.Position &&
      terrainMap.TryGetBuilding(mountainTile.Position, out var placedQuarry) &&
      ReferenceEquals(placedQuarry, worldSettlement.Buildings[0]),
    "World occupancy references the same building object as the settlement");
Check(worldState.Get(Resource.Wood) == 80 && worldState.Get(Resource.Coins) == 85,
    "Spatial construction charges Quarry cost exactly once");
Check(BuildingPlacementRules.Evaluate(terrainMap, mountainTile.Position, cottageDefinition)
      .Failure == PlacementFailure.AlreadyOccupied,
    "Previously occupied mountain plot rejects overlapping structures");
Check(!worldSettlement.TryStartConstruction(
      worldState, terrainMap, mountainTile.Position, BuildingCatalog.QuarryId) &&
      worldState.Get(Resource.Wood) == 80 &&
      worldSettlement.Buildings.Count == 1,
    "Overlapping construction neither charges resources nor creates another building");
Check(worldSettlement.TryStartConstruction(
      worldState, terrainMap, grassTile.Position, BuildingCatalog.CottageId),
    "Spatial Cottage construction succeeds on central grassland");
Check(worldState.Get(Resource.Wood) == 62 && worldState.Get(Resource.Stone) == 20 &&
      worldState.Get(Resource.Coins) == 75 && terrainMap.OccupiedSites.Count == 2,
    "Combined spatial construction updates stockpile and occupancy");
Check(!worldSettlement.TryStartConstruction(
      worldState, terrainMap, waterTile.Position, BuildingCatalog.CottageId) &&
      !worldSettlement.TryStartConstruction(
          worldState, terrainMap, forestTile.Position, BuildingCatalog.CottageId),
    "Invalid terrain fails without placing extra housing");
Check(worldSettlement.Buildings.Count == 2 && worldState.Get(Resource.Wood) == 62,
    "Invalid placement leaves inventory and building list unchanged");

var poorMap = IslandGenerator.Generate(4);
var poorSettlement = new Settlement(population: 8);
var poorState = new GameState(wood: 0, stone: 0, food: 50, coins: 0);
var affordableSite = poorMap.Tiles.First(t => BuildingPlacementRules.Evaluate(
    poorMap, t.Position, cottageDefinition).Allowed);
Check(!poorSettlement.TryStartConstruction(
      poorState, poorMap, affordableSite.Position, BuildingCatalog.CottageId) &&
      poorMap.OccupiedSites.Count == 0 && poorSettlement.Buildings.Count == 0,
    "Unaffordable spatial building does not reserve land or charge resources");

// Upgrading retains the building's original position and occupant reference.
engine.EndTurn(worldState, worldSettlement);
Check(worldSettlement.TryStartUpgrade(worldState, 1),
    "Existing spatial Cottage can be upgraded");
engine.EndTurn(worldState, worldSettlement);
engine.EndTurn(worldState, worldSettlement);
Check(worldSettlement.Buildings[1].Definition.Id == BuildingCatalog.TownhouseId &&
      worldSettlement.Buildings[1].Position == grassTile.Position &&
      terrainMap.TryGetBuilding(grassTile.Position, out var upgradedBuilding) &&
      ReferenceEquals(upgradedBuilding, worldSettlement.Buildings[1]) &&
      terrainMap.OccupiedSites.Count == 2,
    "Townhouse upgrade preserves site identity without duplicating occupancy");


var roadTiles = Enumerable.Range(0, 5)
    .SelectMany(z => Enumerable.Range(0, 7).Select(x =>
        new IslandTile(new GridPosition(x, z),
            x == 0 && z == 0 ? TerrainKind.Water :
            x == 5 && z == 4 ? TerrainKind.Forest :
            x == 6 && z == 4 ? TerrainKind.Mountain :
            TerrainKind.Grassland)));
var roadMap = new IslandMap(7, 5, roadTiles);
var roads = roadMap.Roads;
Check(roads.Segments.Count == 0 && roadMap.Roads == roads,
    "Island owns one initially empty road network");
Check(roads.EvaluateDirtPlacement(new GridPosition(-1, 1)).Failure ==
      RoadFailure.OutsideMap &&
      roads.PlaceDirt(new GridPosition(-1, 1)).Failure == RoadFailure.OutsideMap,
    "Road construction rejects outside-map positions");
Check(roads.PlaceDirt(new GridPosition(0, 0)).Failure == RoadFailure.Water &&
      roads.PlaceDirt(new GridPosition(5, 4)).Failure == RoadFailure.RequiresFlatLand &&
      roads.PlaceDirt(new GridPosition(6, 4)).Failure == RoadFailure.RequiresFlatLand &&
      roads.Segments.Count == 0,
    "Dirt roads cannot be placed on water, forests or mountains");

var noResources = new GameState(wood: 0, stone: 0, food: 0, coins: 0);
Check(roads.PlaceDirt(new GridPosition(1, 1)).Succeeded &&
      roads.Segments[new GridPosition(1, 1)] == RoadSurface.Dirt &&
      noResources.Get(Resource.Stone) == 0,
    "Dirt road construction is free even without any resources");
Check(roads.PlaceDirt(new GridPosition(1, 1)).Failure == RoadFailure.RoadAlreadyPresent,
    "Dirt roads cannot be placed twice on the same tile");
Check(roads.EvaluateStoneUpgrade(noResources, new GridPosition(1, 1)).Failure ==
      RoadFailure.InsufficientStone &&
      roads.UpgradeToStone(noResources, new GridPosition(1, 1)).Failure ==
      RoadFailure.InsufficientStone &&
      roads.Segments[new GridPosition(1, 1)] == RoadSurface.Dirt,
    "Unaffordable stone upgrade neither consumes resources nor changes road");
Check(roads.UpgradeToStone(noResources, new GridPosition(3, 3)).Failure ==
      RoadFailure.RoadMissing &&
      roads.UpgradeToStone(noResources, new GridPosition(10, 10)).Failure ==
      RoadFailure.OutsideMap,
    "Stone upgrades require an existing, in-bounds dirt road");

var roadState = new GameState(wood: 100, stone: 20, food: 100, coins: 100);
var roadVillage = new Settlement(population: 8);
var firstHousePosition = new GridPosition(1, 2);
var secondHousePosition = new GridPosition(4, 2);
Check(roadVillage.TryStartConstruction(
        roadState, roadMap, firstHousePosition, BuildingCatalog.CottageId) &&
      roadVillage.TryStartConstruction(
        roadState, roadMap, secondHousePosition, BuildingCatalog.CottageId),
    "Two cottages can be placed on independent land tiles");
Check(roads.PlaceDirt(firstHousePosition).Failure == RoadFailure.BuildingOccupied &&
      roads.Segments.Count == 1,
    "Roads cannot replace an occupied building plot");
Check(roads.PlaceDirt(new GridPosition(2, 1)).Succeeded &&
      roads.PlaceDirt(new GridPosition(4, 1)).Succeeded &&
      roads.PlaceDirt(new GridPosition(2, 0)).Succeeded,
    "Dirt roads may be painted as disconnected segments and branches");
Check(!roads.AreConnected(new GridPosition(1, 1), new GridPosition(4, 1)) &&
      !roads.AreBuildingsConnected(firstHousePosition, secondHousePosition),
    "Separated road components do not connect buildings");
Check(roads.IsBuildingRoadConnected(firstHousePosition) &&
      roads.IsBuildingRoadConnected(secondHousePosition) &&
      !roads.IsBuildingRoadConnected(new GridPosition(3, 4)),
    "Road adjacency is reported only for real buildings");
Check(roads.PlaceDirt(new GridPosition(3, 1)).Succeeded &&
      roads.AreConnected(new GridPosition(1, 1), new GridPosition(4, 1)) &&
      roads.AreBuildingsConnected(firstHousePosition, secondHousePosition) &&
      roads.ConnectedComponent(new GridPosition(1, 1)).Count == 5,
    "One bridging segment joins road branches and connects the two cottages");
Check(roads.PlaceDirt(new GridPosition(6, 1)).Succeeded &&
      !roads.AreConnected(new GridPosition(1, 1), new GridPosition(6, 1)) &&
      roads.ConnectedComponent(new GridPosition(6, 1)).Count == 1 &&
      roads.ConnectedComponent(new GridPosition(5, 3)).Count == 0,
    "Disconnected road islands and missing roads are distinguished");
Check(roads.AreConnected(new GridPosition(1, 1), new GridPosition(1, 1)) &&
      !roads.AreConnected(new GridPosition(1, 1), firstHousePosition),
    "Only actual road positions participate in connectivity");

var blockedRoadPosition = new GridPosition(3, 1);
var woodBeforeBlocked = roadState.Get(Resource.Wood);
Check(BuildingPlacementRules.Evaluate(roadMap, blockedRoadPosition, cottageDefinition).Failure ==
      PlacementFailure.RoadPresent &&
      !roadVillage.TryStartConstruction(
          roadState, roadMap, blockedRoadPosition, BuildingCatalog.CottageId) &&
      roadState.Get(Resource.Wood) == woodBeforeBlocked &&
      roadVillage.Buildings.Count == 2,
    "Construction cannot overwrite roads or charge for an invalid building");

var stoneBefore = roadState.Get(Resource.Stone);
Check(roads.EvaluateStoneUpgrade(roadState, new GridPosition(2, 1)).Succeeded &&
      roads.UpgradeToStone(roadState, new GridPosition(2, 1)).Succeeded &&
      roadState.Get(Resource.Stone) == stoneBefore - IslandRoadNetwork.StoneUpgradeCost &&
      roads.Segments[new GridPosition(2, 1)] == RoadSurface.Stone,
    "Stone-road upgrades spend exactly two stone and retain their grid location");
Check(roads.UpgradeToStone(roadState, new GridPosition(2, 1)).Failure ==
      RoadFailure.AlreadyStone &&
      roadState.Get(Resource.Stone) == stoneBefore - IslandRoadNetwork.StoneUpgradeCost,
    "Repeated stone upgrades are rejected without duplicate spending");
Check(roads.AreBuildingsConnected(firstHousePosition, secondHousePosition),
    "Upgrading a dirt segment to stone preserves road connectivity");

// The guaranteed mountain approach is connectable without cutting through
// a mountain, forest or another occupied building.
var roadIsland = IslandGenerator.Generate(42);
var mountain = new GridPosition(roadIsland.Width / 2 + 2, roadIsland.Height / 2 - 1);
var approach = new GridPosition(roadIsland.Width / 2 + 1, roadIsland.Height / 2 - 1);
var corridor = new GridPosition(roadIsland.Width / 2 + 1, roadIsland.Height / 2);
var townCenter = new GridPosition(roadIsland.Width / 2, roadIsland.Height / 2);
var roadIslandState = new GameState(wood: 100, stone: 30, food: 100, coins: 100);
var roadIslandSettlement = new Settlement(population: 8);
Check(roadIslandSettlement.TryStartConstruction(
        roadIslandState, roadIsland, mountain, BuildingCatalog.QuarryId) &&
      roadIslandSettlement.TryStartConstruction(
        roadIslandState, roadIsland, townCenter, BuildingCatalog.CottageId),
    "Generated mountain Quarry and central Cottage can coexist");
Check(roadIsland.Roads.PlaceDirt(approach).Succeeded &&
      roadIsland.Roads.PlaceDirt(corridor).Succeeded &&
      roadIsland.Roads.AreBuildingsConnected(townCenter, mountain),
    "Guaranteed grass approach supports a road-connected mountain quarry");


var diagonalTiles = Enumerable.Range(0, 3).SelectMany(z =>
    Enumerable.Range(0, 3).Select(x =>
        new IslandTile(new GridPosition(x, z), TerrainKind.Grassland)));
var diagonalIsland = new IslandMap(3, 3, diagonalTiles);
Check(diagonalIsland.Roads.PlaceDirt(new GridPosition(0, 0)).Succeeded &&
      diagonalIsland.Roads.PlaceDirt(new GridPosition(1, 1)).Succeeded &&
      !diagonalIsland.Roads.AreConnected(new GridPosition(0, 0), new GridPosition(1, 1)),
    "Diagonally touching road segments are not connected");
Check(diagonalIsland.Roads.PlaceDirt(new GridPosition(0, 1)).Succeeded &&
      diagonalIsland.Roads.AreConnected(new GridPosition(0, 0), new GridPosition(1, 1)),
    "Orthogonal connector closes a diagonal gap");

StrategicEconomyTests.Run(Check);

Console.WriteLine("All smoke tests passed.");
Console.WriteLine(RoadPathChecks.Run());
