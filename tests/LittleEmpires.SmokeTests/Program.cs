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
Console.WriteLine("All smoke tests passed.");
