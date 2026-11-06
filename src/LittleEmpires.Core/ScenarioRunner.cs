namespace LittleEmpires.Core;

/// <summary>Start construction before resolving the turn at this zero-based index.</summary>
public sealed record PlannedConstruction(int TurnIndex, string BuildingId);

public sealed record ConstructionResult(string BuildingId, bool Started);

/// <summary>Snapshot of one simulated turn, including the final stockpile.</summary>
public sealed record SeasonReport(
    int TurnIndex,
    TurnSummary Turn,
    IReadOnlyList<ConstructionResult> Construction,
    IReadOnlyDictionary<Resource, int> EndingResources);

/// <summary>
/// Runs a deterministic economy scenario using the same TurnEngine as gameplay.
/// This is a balancing tool, not an AI player or an alternative game rules engine.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly TurnEngine _engine = new();

    public IReadOnlyList<SeasonReport> Run(
        GameState state,
        Settlement settlement,
        int seasons,
        IEnumerable<PlannedConstruction>? constructionPlan = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);
        if (seasons < 1) throw new ArgumentOutOfRangeException(nameof(seasons));

        var plan = (constructionPlan ?? Enumerable.Empty<PlannedConstruction>()).ToArray();
        foreach (var construction in plan)
        {
            if (construction is null)
                throw new ArgumentException("Construction plan contains a null entry.", nameof(constructionPlan));
            if (construction.TurnIndex < 0 || construction.TurnIndex >= seasons)
                throw new ArgumentOutOfRangeException(nameof(constructionPlan),
                    "Construction turn must be within the simulation range.");
            // Fail early rather than partially simulating an invalid scenario.
            BuildingCatalog.Get(construction.BuildingId);
        }

        var scheduledByTurn = plan.GroupBy(item => item.TurnIndex)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var reports = new List<SeasonReport>(seasons);
        for (var turnIndex = 0; turnIndex < seasons; turnIndex++)
        {
            var attempts = new List<ConstructionResult>();
            if (scheduledByTurn.TryGetValue(turnIndex, out var orders))
            {
                foreach (var order in orders)
                {
                    var started = settlement.TryStartConstruction(state, order.BuildingId);
                    attempts.Add(new ConstructionResult(order.BuildingId, started));
                }
            }

            var turn = _engine.EndTurn(state, settlement);
            var stockpile = state.Resources.ToDictionary(entry => entry.Key, entry => entry.Value);
            reports.Add(new SeasonReport(turnIndex, turn, attempts.AsReadOnly(), stockpile));
        }

        return reports.AsReadOnly();
    }
}
