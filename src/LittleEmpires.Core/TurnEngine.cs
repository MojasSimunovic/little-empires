namespace LittleEmpires.Core;

/// <summary>A report for one completed seasonal turn.</summary>
public sealed record TurnSummary(
    int StartingYear,
    Season StartingSeason,
    int EndingYear,
    Season EndingSeason,
    IReadOnlyDictionary<Resource, int> ResourceChanges,
    IReadOnlyList<string> CompletedBuildings,
    int Population,
    int FoodRequired,
    int FoodConsumed,
    int FoodShortage);

/// <summary>
/// Resolves one season. Settlement handles production and construction before
/// GameState advances the calendar. New buildings produce from the next turn.
/// </summary>
public sealed class TurnEngine
{
    public TurnSummary EndTurn(GameState state, Settlement settlement)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);

        var startingYear = state.Year;
        var startingSeason = state.Season;
        var before = Enum.GetValues<Resource>()
            .ToDictionary(resource => resource, state.Get);
        var incomplete = settlement.Buildings
            .Where(building => !building.IsComplete)
            .ToHashSet();

        // Order is intentional: existing production can feed residents this turn.
        settlement.Produce(state);
        var foodRequired = settlement.Population;
        var foodConsumed = Math.Min(foodRequired, state.Get(Resource.Food));
        if (foodConsumed > 0)
            state.TrySpend(new Dictionary<Resource, int> { [Resource.Food] = foodConsumed });
        var foodShortage = foodRequired - foodConsumed;
        settlement.ProgressConstruction();
        state.AdvanceSeason();

        var changes = Enum.GetValues<Resource>()
            .ToDictionary(resource => resource, resource => state.Get(resource) - before[resource]);
        var completed = settlement.Buildings
            .Where(building => incomplete.Contains(building) && building.IsComplete)
            .Select(building => building.Definition.Name)
            .ToArray();

        return new TurnSummary(
            startingYear, startingSeason, state.Year, state.Season,
            changes, completed, settlement.Population,
            foodRequired, foodConsumed, foodShortage);
    }
}
