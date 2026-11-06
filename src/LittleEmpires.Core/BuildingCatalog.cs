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

    private static readonly IReadOnlyList<BuildingDefinition> Definitions =
        Array.AsReadOnly(new[]
        {
            new BuildingDefinition(
                LumberCampId, "Lumber Camp",
                Map((Resource.Wood, 15), (Resource.Coins, 10)),
                2, Map((Resource.Wood, 6))),
            new BuildingDefinition(
                FisheryId, "Fishery",
                Map((Resource.Wood, 12), (Resource.Stone, 3), (Resource.Coins, 8)),
                1, Map((Resource.Food, 5))),
            new BuildingDefinition(
                QuarryId, "Quarry",
                Map((Resource.Wood, 20), (Resource.Coins, 15)),
                2, Map((Resource.Stone, 4))),
            new BuildingDefinition(
                FarmId, "Farm",
                Map((Resource.Wood, 25), (Resource.Stone, 5), (Resource.Coins, 12)),
                2, Map((Resource.Food, 9)))
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
