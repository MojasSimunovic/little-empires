using System;
using System.Collections.Generic;
using System.Linq;

using System.Collections.ObjectModel;

namespace LittleEmpires.Core
{

/// <summary>
/// Upgrades transform an existing building in place, preserving its housing
/// capacity while work is in progress. Cost and duration are upgrade-specific.
/// </summary>
public sealed record BuildingUpgradeDefinition(
    string FromBuildingId,
    string ToBuildingId,
    IReadOnlyDictionary<Resource, int> Cost,
    int ConstructionSeasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FromBuildingId) ||
            string.IsNullOrWhiteSpace(ToBuildingId) ||
            FromBuildingId == ToBuildingId)
            throw new ArgumentException("Upgrade must have different source and target building IDs.");
        if (ConstructionSeasons < 1)
            throw new ArgumentOutOfRangeException(nameof(ConstructionSeasons));
        if (Cost.Values.Any(amount => amount < 0))
            throw new ArgumentException("Upgrade costs cannot be negative.");

        var from = BuildingCatalog.Get(FromBuildingId);
        var to = BuildingCatalog.Get(ToBuildingId);
        if (from.HousingCapacity <= 0 || to.HousingCapacity <= from.HousingCapacity)
            throw new ArgumentException("Housing upgrade must increase capacity.");
    }
}

public static class HousingUpgradeCatalog
{
    public static readonly BuildingUpgradeDefinition CottageToTownhouse = new(
        BuildingCatalog.CottageId,
        BuildingCatalog.TownhouseId,
        new ReadOnlyDictionary<Resource, int>(new Dictionary<Resource, int>
        {
            [Resource.Wood] = 24,
            [Resource.Stone] = 10,
            [Resource.Coins] = 20
        }),
        ConstructionSeasons: 2);

    private static readonly IReadOnlyDictionary<string, BuildingUpgradeDefinition> BySource =
        new ReadOnlyDictionary<string, BuildingUpgradeDefinition>(
            new Dictionary<string, BuildingUpgradeDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [CottageToTownhouse.FromBuildingId] = CottageToTownhouse
            });

    public static bool TryGet(string? fromBuildingId, out BuildingUpgradeDefinition? upgrade)
    {
        if (string.IsNullOrWhiteSpace(fromBuildingId))
        {
            upgrade = null;
            return false;
        }

        return BySource.TryGetValue(fromBuildingId, out upgrade);
    }
}
}
