using System.Collections.ObjectModel;

namespace LittleEmpires.Core;

/// <summary>Seasonal output totals, separated from net resource changes.</summary>
public sealed record ProductionReport(IReadOnlyDictionary<Resource, int> Produced);

/// <summary>
/// Existing production buildings operate this turn; newly completed buildings
/// still begin next turn. Industrial bonuses require operational processors
/// connected by roads, within six Manhattan tiles, and never stack.
/// </summary>
public static class ProductionSystem
{
    public const int BoostRadius = 6;
    public const int LumberMillBonusPercent = 50;
    public const int FoodProcessorBonusPercent = 25;

    public static ProductionReport Resolve(
        GameState state, Settlement settlement, IslandMap? island,
        WorkforceReport staffing)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(staffing);

        var totals = new Dictionary<Resource, long>();
        foreach (var source in settlement.Buildings.Where(building => building.IsComplete))
        {
            if (settlement.EconomyMode == EconomyMode.Strategic &&
                !staffing.IsOperational(source))
                continue;

            var demand = source.Definition.WorkersRequired;
            var assigned = settlement.EconomyMode == EconomyMode.Classic || demand == 0
                ? demand
                : staffing.AssignedTo(source);

            foreach (var (resource, baseOutput) in source.Definition.ProductionPerSeason)
            {
                // Fully staffed or Classic buildings produce their base output;
                // partially staffed buildings produce proportionally (floor).
                var output = demand == 0
                    ? baseOutput
                    : checked((int)((long)baseOutput * assigned / demand));
                if (output == 0) continue;

                var percentage = BonusPercent(source, resource, settlement, island, staffing);
                var bonus = checked((int)((long)output * percentage / 100));
                totals[resource] = checked(totals.GetValueOrDefault(resource) + output + bonus);
            }
        }

        // All amounts are checked before changing the inventory.
        foreach (var (resource, total) in totals)
        {
            if (total > int.MaxValue || state.Get(resource) > int.MaxValue - total)
                throw new OverflowException("Seasonal production exceeds resource capacity.");
        }
        foreach (var (resource, total) in totals)
            state.Add(resource, checked((int)total));

        return new ProductionReport(new ReadOnlyDictionary<Resource, int>(
            totals.ToDictionary(entry => entry.Key, entry => checked((int)entry.Value))));
    }

    private static int BonusPercent(
        Building source, Resource resource, Settlement settlement,
        IslandMap? island, WorkforceReport staffing)
    {
        if (island is null || source.Position is null) return 0;

        var requiredProcessor = (source.Definition.Id, resource) switch
        {
            (BuildingCatalog.LumberCampId, Resource.Wood) => BuildingCatalog.LumberMillId,
            (BuildingCatalog.FarmId, Resource.Food) => BuildingCatalog.FoodProcessorId,
            (BuildingCatalog.FisheryId, Resource.Food) => BuildingCatalog.FoodProcessorId,
            _ => null
        };
        if (requiredProcessor is null) return 0;

        foreach (var processor in settlement.Buildings)
        {
            if (!processor.IsComplete || processor.Position is null ||
                processor.Definition.Id != requiredProcessor)
                continue;
            if (settlement.EconomyMode == EconomyMode.Strategic &&
                !staffing.IsOperational(processor))
                continue;

            var a = source.Position.Value;
            var b = processor.Position.Value;
            if (Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) > BoostRadius)
                continue;
            if (!island.Roads.AreBuildingsConnected(a, b))
                continue;

            return requiredProcessor == BuildingCatalog.LumberMillId
                ? LumberMillBonusPercent : FoodProcessorBonusPercent;
        }

        return 0;
    }
}
