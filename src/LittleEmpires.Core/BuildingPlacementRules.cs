namespace LittleEmpires.Core;

/// <summary>Requirements for a building's placement on an island grid.</summary>
public enum PlacementRequirement
{
    FlatLand,
    FertileLand,
    Mountain,
    ForestEdge,
    Shoreline
}

public enum PlacementFailure
{
    None,
    OutsideMap,
    Water,
    AlreadyOccupied,
    UpgradeOnly,
    RequiresFlatLand,
    RequiresFertileLand,
    RequiresMountain,
    RequiresForestNearby,
    RequiresShoreline
}

public readonly record struct PlacementResult(PlacementFailure Failure)
{
    public bool Allowed => Failure == PlacementFailure.None;
}

/// <summary>
/// Evaluates spatial construction rules without spending resources or changing
/// the island. A Unity placement preview can call this same validator.
/// </summary>
public static class BuildingPlacementRules
{
    public static PlacementResult Evaluate(
        IslandMap island, GridPosition position, BuildingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(island);
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.CanBuildDirectly)
            return new PlacementResult(PlacementFailure.UpgradeOnly);
        if (!island.TryGetTile(position, out var tile))
            return new PlacementResult(PlacementFailure.OutsideMap);
        if (tile.Terrain == TerrainKind.Water)
            return new PlacementResult(PlacementFailure.Water);
        if (island.OccupiedSites.ContainsKey(position))
            return new PlacementResult(PlacementFailure.AlreadyOccupied);

        var flatLand = tile.Terrain is TerrainKind.Grassland or TerrainKind.FertileLand;
        var failure = definition.PlacementRequirement switch
        {
            PlacementRequirement.FlatLand =>
                flatLand ? PlacementFailure.None : PlacementFailure.RequiresFlatLand,

            PlacementRequirement.FertileLand =>
                tile.Terrain == TerrainKind.FertileLand
                    ? PlacementFailure.None : PlacementFailure.RequiresFertileLand,

            PlacementRequirement.Mountain =>
                tile.Terrain == TerrainKind.Mountain
                    ? PlacementFailure.None : PlacementFailure.RequiresMountain,

            PlacementRequirement.ForestEdge =>
                !flatLand ? PlacementFailure.RequiresFlatLand :
                island.Neighbours(position).Any(t => t.Terrain == TerrainKind.Forest)
                    ? PlacementFailure.None : PlacementFailure.RequiresForestNearby,

            PlacementRequirement.Shoreline =>
                !flatLand ? PlacementFailure.RequiresFlatLand :
                island.IsShoreline(position)
                    ? PlacementFailure.None : PlacementFailure.RequiresShoreline,

            _ => throw new ArgumentOutOfRangeException(
                nameof(definition), "Unknown building placement requirement.")
        };

        return new PlacementResult(failure);
    }
}
