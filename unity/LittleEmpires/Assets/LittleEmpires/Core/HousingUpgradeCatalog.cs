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
    int ConstructionSeasons,
    int MinimumPopulation = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FromBuildingId) ||
            string.IsNullOrWhiteSpace(ToBuildingId) ||
            FromBuildingId == ToBuildingId)
            throw new ArgumentException("Upgrade must have different source and target building IDs.");
        if (ConstructionSeasons < 1)
            throw new ArgumentOutOfRangeException(nameof(ConstructionSeasons));
        if (MinimumPopulation < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumPopulation));
        if (Cost.Values.Any(amount => amount < 0))
            throw new ArgumentException("Upgrade costs cannot be negative.");

        var from = BuildingCatalog.Get(FromBuildingId);
        var to = BuildingCatalog.Get(ToBuildingId);
        // Housing must not be lost. Non-housing civic buildings reuse this path.
        if (to.HousingCapacity < from.HousingCapacity ||
            (from.HousingCapacity > 0 && to.HousingCapacity == from.HousingCapacity))
            throw new ArgumentException("Housing upgrades must increase capacity.");
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
        ConstructionSeasons: 2,
        MinimumPopulation: 20);

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

    
/// <summary>Civic upgrades share the same in-place construction system as housing.</summary>
public static class CivicUpgradeCatalog
{
    // Provisional balance, to be tuned through playtesting.
    public static readonly BuildingUpgradeDefinition VillageHallToTownHall = new(
        BuildingCatalog.VillageHallId,
        BuildingCatalog.TownHallId,
        new ReadOnlyDictionary<Resource, int>(new Dictionary<Resource, int>
        {
            [Resource.Wood] = 60,
            [Resource.Stone] = 25,
            [Resource.Coins] = 300
        }),
        ConstructionSeasons: 2,
        MinimumPopulation: 50);

    public static bool TryGet(string? fromBuildingId, out BuildingUpgradeDefinition? upgrade)
    {
        if (string.Equals(fromBuildingId, VillageHallToTownHall.FromBuildingId,
                StringComparison.OrdinalIgnoreCase))
        {
            upgrade = VillageHallToTownHall;
            return true;
        }

        upgrade = null;
        return false;
    }
}

/// <summary>Single lookup for all supported upgrade paths.</summary>
public static class BuildingUpgradeCatalog
{
    public static bool TryGet(string? fromBuildingId, out BuildingUpgradeDefinition? upgrade) =>
        HousingUpgradeCatalog.TryGet(fromBuildingId, out upgrade) ||
        CivicUpgradeCatalog.TryGet(fromBuildingId, out upgrade);
}

}
