using System.Collections.ObjectModel;

namespace LittleEmpires.Core;

/// <summary>
/// Starting building catalogue for the Little Empires prototype.
/// Values are provisional and can be rebalanced without changing simulation logic.
/// </summary>
public static class BuildingCatalog
{
    public const string LumberCampId = "lumber-camp";
    public const string FisheryId = "fishery";
    public const string QuarryId = "quarry";
    public const string FarmId = "farm";
    public const string CottageId = "cottage";
    public const string TownhouseId = "townhouse";
    public const string LumberMillId = "lumber-mill";
    public const string FoodProcessorId = "food-processor";
    public const string MarketplaceId = "marketplace";

    private static readonly IReadOnlyList<BuildingDefinition> Definitions =
        Array.AsReadOnly(new[]
        {
            new BuildingDefinition(
                LumberCampId, "Lumber Camp",
                Map((Resource.Wood, 15), (Resource.Coins, 10)),
                2, Map((Resource.Wood, 6)), SiteRequirement: PlacementRequirement.ForestEdge,
                WorkersRequired: 3, UpkeepCoinsPerSeason: 2),
            new BuildingDefinition(
                FisheryId, "Fishery",
                Map((Resource.Wood, 12), (Resource.Stone, 3), (Resource.Coins, 8)),
                1, Map((Resource.Food, 5)), SiteRequirement: PlacementRequirement.Shoreline,
                WorkersRequired: 3, UpkeepCoinsPerSeason: 2),
            new BuildingDefinition(
                QuarryId, "Quarry",
                Map((Resource.Wood, 20), (Resource.Coins, 15)),
                2, Map((Resource.Stone, 4)), SiteRequirement: PlacementRequirement.Mountain,
                WorkersRequired: 4, UpkeepCoinsPerSeason: 3),
            new BuildingDefinition(
                FarmId, "Farm",
                Map((Resource.Wood, 25), (Resource.Stone, 5), (Resource.Coins, 12)),
                2, Map((Resource.Food, 9)), SiteRequirement: PlacementRequirement.FertileLand,
                WorkersRequired: 4, UpkeepCoinsPerSeason: 3),
            new BuildingDefinition(
                CottageId, "Cottage",
                Map((Resource.Wood, 18), (Resource.Stone, 5), (Resource.Coins, 10)),
                1, Map(), HousingCapacity: 6),
            // Upgrade destination only: never charge this definition's empty cost
            // as a direct construction price. The upgrade has its own cost/time.
            new BuildingDefinition(
                TownhouseId, "Townhouse", Map(), 1, Map(),
                HousingCapacity: 12, CanBuildDirectly: false),
            new BuildingDefinition(
                LumberMillId, "Lumber Mill",
                Map((Resource.Wood, 35), (Resource.Stone, 15), (Resource.Coins, 30)),
                2, Map(), WorkersRequired: 2, UpkeepCoinsPerSeason: 4),
            new BuildingDefinition(
                FoodProcessorId, "Food Processing Plant",
                Map((Resource.Wood, 40), (Resource.Stone, 20), (Resource.Coins, 40)),
                3, Map(), WorkersRequired: 3, UpkeepCoinsPerSeason: 5),
            new BuildingDefinition(
                MarketplaceId, "Marketplace",
                Map((Resource.Wood, 25), (Resource.Stone, 10), (Resource.Coins, 20)),
                2, Map())
        });

    private static readonly IReadOnlyDictionary<string, BuildingDefinition> ById =
        new ReadOnlyDictionary<string, BuildingDefinition>(
            Definitions.ToDictionary(building => building.Id,
                building => building, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<BuildingDefinition> All => Definitions;

    public static bool TryGet(string? buildingId, out BuildingDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            definition = null;
            return false;
        }

        return ById.TryGetValue(buildingId, out definition);
    }

    public static BuildingDefinition Get(string buildingId) =>
        TryGet(buildingId, out var definition)
            ? definition!
            : throw new KeyNotFoundException($"Unknown building ID: '{buildingId}'.");

    private static IReadOnlyDictionary<Resource, int> Map(
        params (Resource Resource, int Amount)[] values) =>
        new ReadOnlyDictionary<Resource, int>(
            values.ToDictionary(value => value.Resource, value => value.Amount));
}
