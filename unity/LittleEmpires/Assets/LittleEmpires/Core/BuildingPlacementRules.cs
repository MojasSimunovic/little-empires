using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

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
    RoadPresent,
    UpgradeOnly,
    RequiresFlatLand,
    RequiresFertileLand,
    RequiresMountain,
    RequiresForestNearby,
    RequiresShoreline,
    RequiresLevelSite
}

public readonly struct PlacementResult
{
    public PlacementFailure Failure { get; }
    public PlacementResult(PlacementFailure failure) { Failure = failure; }
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
        if (island == null) throw new ArgumentNullException(nameof(island));
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        if (!definition.CanBuildDirectly)
            return new PlacementResult(PlacementFailure.UpgradeOnly);
        if (!island.TryGetTile(position, out var tile))
            return new PlacementResult(PlacementFailure.OutsideMap);
        if (tile.Terrain == TerrainKind.Water || tile.Terrain == TerrainKind.River)
            return new PlacementResult(PlacementFailure.Water);
        if (island.OccupiedSites.ContainsKey(position))
            return new PlacementResult(PlacementFailure.AlreadyOccupied);
        if (island.Roads.HasRoad(position))
            return new PlacementResult(PlacementFailure.RoadPresent);

        var flatLand = tile.Terrain is TerrainKind.Grassland or TerrainKind.FertileLand;
        var failure = definition.SiteRequirement switch
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

        // A terrain type alone does not guarantee a level physical foundation.
        // The real 64x64 Unity island has slopes at the coast and mountain;
        // reject those plots instead of floating a building from its center height.
        if (failure == PlacementFailure.None && island.Width == 64 && island.Height == 64 &&
            LargeIslandGeography.BuildingFootprintRelief(position) >
                LargeIslandGeography.MaximumBuildingRelief)
            return new PlacementResult(PlacementFailure.RequiresLevelSite);

        return new PlacementResult(failure);
    }
}
}
