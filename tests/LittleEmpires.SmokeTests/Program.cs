using LittleEmpires.Core;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var state = new GameState();
var settlement = new Settlement();
var lumberCamp = new BuildingDefinition(
    "lumber-camp", "Lumber Camp",
    new Dictionary<Resource, int> { [Resource.Wood] = 20 },
    2,
    new Dictionary<Resource, int> { [Resource.Wood] = 5 });

Check(settlement.TryStartConstruction(state, lumberCamp), "Can build when affordable");
Check(state.Get(Resource.Wood) == 30, "Construction cost paid upfront");
Check(!settlement.Buildings[0].IsComplete, "Building starts under construction");

settlement.ResolveSeason(state);
Check(state.Season == Season.Summer, "Season advances");
Check(state.Get(Resource.Wood) == 30, "No production during construction");

settlement.ResolveSeason(state);
Check(settlement.Buildings[0].IsComplete, "Building completes after two seasons");
Check(state.Get(Resource.Wood) == 30, "No production on completion season");

settlement.ResolveSeason(state);
Check(state.Get(Resource.Wood) == 35, "Completed building produces next season");

settlement.ResolveSeason(state);
Check(state.Season == Season.Spring && state.Year == 2, "Winter advances to next year");

var expensive = lumberCamp with { Cost = new Dictionary<Resource, int> { [Resource.Wood] = 1000 } };
Check(!settlement.TryStartConstruction(state, expensive), "Cannot build without resources");
Check(settlement.Buildings.Count == 1, "Failed construction does not create a building");
Console.WriteLine("All smoke tests passed.");
