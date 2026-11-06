using System.Collections.ObjectModel;

namespace LittleEmpires.Core;

/// <summary>Staffing allocation for the current turn, keyed by actual buildings.</summary>
public sealed class WorkforceReport
{
    private readonly IReadOnlyDictionary<Building, int> _staffing;
    public IReadOnlyDictionary<Building, int> AssignedWorkers => _staffing;
    public int WorkingResidents { get; }
    public int UnemployedResidents { get; }
    public int OperatingCosts { get; }

    internal WorkforceReport(
        IReadOnlyDictionary<Building, int> staffing,
        int workingResidents, int unemployedResidents, int operatingCosts)
    {
        _staffing = staffing;
        WorkingResidents = workingResidents;
        UnemployedResidents = unemployedResidents;
        OperatingCosts = operatingCosts;
    }

    public int AssignedTo(Building building) =>
        _staffing.TryGetValue(building, out var workers) ? workers : 0;

    public bool IsOperational(Building building) =>
        building.IsComplete &&
        (building.Definition.WorkersRequired == 0 || AssignedTo(building) > 0);
}

/// <summary>
/// Deterministic allocation by descending work priority, then building order.
/// Partial staffing produces proportionally fewer goods; an active building
/// pays its whole per-season upkeep (no fractional currency).
/// </summary>
public static class WorkforceSystem
{
    public static WorkforceReport Resolve(GameState state, Settlement settlement)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);

        var available = settlement.Population;
        var spent = 0;
        var allocations = new Dictionary<Building, int>();

        // Stable tie ordering is vital for deterministic, replayable turns.
        foreach (var building in settlement.Buildings
                     .Where(b => b.IsComplete && b.Definition.WorkersRequired > 0)
                     .OrderByDescending(b => b.WorkPriority))
        {
            var desired = building.Definition.WorkersRequired;
            var count = Math.Min(desired, available);
            var upkeep = building.Definition.UpkeepCoinsPerSeason;
            if (count == 0 || state.Get(Resource.Coins) < upkeep)
            {
                allocations[building] = 0;
                continue;
            }

            if (upkeep != 0 &&
                !state.TrySpend(new Dictionary<Resource, int> { [Resource.Coins] = upkeep }))
                throw new InvalidOperationException("Operating cost changed during allocation.");

            allocations[building] = count;
            available -= count;
            spent = checked(spent + upkeep);
        }

        return new WorkforceReport(
            new ReadOnlyDictionary<Building, int>(allocations),
            settlement.Population - available, available, spent);
    }

    internal static WorkforceReport Classic(Settlement settlement) =>
        new(new ReadOnlyDictionary<Building, int>(new Dictionary<Building, int>()),
            0, settlement.Population, 0);
}
