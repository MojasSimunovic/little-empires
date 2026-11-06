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

Console.WriteLine("All smoke tests passed.");
