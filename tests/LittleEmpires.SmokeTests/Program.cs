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
Check(definitions.Count == 4, "Catalogue contains four starter buildings");
Check(definitions.Select(building => building.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4,
    "Every catalogue building has a unique ID");
foreach (var definition in definitions)
{
    definition.Validate();
    Check(definition.ProductionPerSeason.Count > 0 &&
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
foreach (var definition in definitions)
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

Console.WriteLine("All smoke tests passed.");
