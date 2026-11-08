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
    RequiresSettlementTier,
    CivicHallAlreadyBuilt,
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
        if (!island.Contains(position) || definition.Width > island.Width - position.X ||
            definition.Height > island.Height - position.Z)
            return new PlacementResult(PlacementFailure.OutsideMap);
        foreach (var cell in position.Rectangle(definition.Width, definition.Height))
        {
            var failure = EvaluateCell(island, cell, definition.SiteRequirement);
            if (failure != PlacementFailure.None) return new PlacementResult(failure);
        }

        return new PlacementResult(island.BuildingFootprintRelief(position, definition.Width, definition.Height) >
            LargeIslandGeography.MaximumBuildingRelief ? PlacementFailure.RequiresLevelSite : PlacementFailure.None);
    }

    private static PlacementFailure EvaluateCell(IslandMap island, GridPosition position, PlacementRequirement requirement)
    {
        var tile = island.GetTile(position);
        if (tile.Terrain == TerrainKind.Water || tile.Terrain == TerrainKind.River)
            return PlacementFailure.Water;
        if (island.OccupiedSites.ContainsKey(position))
            return PlacementFailure.AlreadyOccupied;
        if (island.Roads.HasRoad(position))
            return PlacementFailure.RoadPresent;

        var flatLand = tile.Terrain is TerrainKind.Grassland or TerrainKind.FertileLand;
        return requirement switch
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
                nameof(requirement), "Unknown building placement requirement.")
        };
    }
}
}
