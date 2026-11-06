namespace LittleEmpires.Core;

/// <summary>A report for one completed seasonal turn.</summary>
public sealed record TurnSummary(
    int StartingYear,
    Season StartingSeason,
    int EndingYear,
    Season EndingSeason,
    IReadOnlyDictionary<Resource, int> ResourceChanges,
    IReadOnlyList<string> CompletedBuildings);

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

        settlement.ResolveSeason(state);

        var changes = Enum.GetValues<Resource>()
            .ToDictionary(resource => resource, resource => state.Get(resource) - before[resource]);
        var completed = settlement.Buildings
            .Where(building => incomplete.Contains(building) && building.IsComplete)
            .Select(building => building.Definition.Name)
            .ToArray();

        return new TurnSummary(
            startingYear, startingSeason, state.Year, state.Season,
            changes, completed);
    }
}
