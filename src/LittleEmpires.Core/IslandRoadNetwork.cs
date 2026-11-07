using System;
using System.Collections.Generic;
using System.Linq;

using System.Collections.ObjectModel;

namespace LittleEmpires.Core
{

/// <summary>Roads occupy a separate layer from the immutable terrain grid.</summary>
public enum RoadSurface { Dirt, Stone }

/// <summary>Describes why a road action cannot be performed.</summary>
public enum RoadFailure
{
    None,
    OutsideMap,
    Water,
    RequiresFlatLand,
    BuildingOccupied,
    RoadAlreadyPresent,
    RoadMissing,
    AlreadyStone,
    InsufficientStone,
    InvalidPath,
    InsufficientWood,
    InvalidCrossing
}

public readonly struct RoadActionResult
{
    public RoadFailure Failure { get; }
    public RoadActionResult(RoadFailure failure) { Failure = failure; }
    public bool Succeeded => Failure == RoadFailure.None;
}

/// <summary>
/// Road grid owned by one IslandMap. A tile can contain either a building or
/// a road, never both. Disconnected segments are allowed, but connectivity
/// is assessed through four-way orthogonal neighbors only.
/// </summary>
public sealed partial class IslandRoadNetwork
{
    public const int StoneUpgradeCost = 2;

    private readonly IslandMap _island;
    private readonly Dictionary<GridPosition, RoadSurface> _roads = new();
    private readonly IReadOnlyDictionary<GridPosition, RoadSurface> _readOnlyRoads;

    internal IslandRoadNetwork(IslandMap island)
    {
        _island = island;
        _readOnlyRoads = new ReadOnlyDictionary<GridPosition, RoadSurface>(_roads);
    }

    public IReadOnlyDictionary<GridPosition, RoadSurface> Segments => _readOnlyRoads;

    public bool HasRoad(GridPosition position) => _roads.ContainsKey(position);

    /// <summary>Pure validation for road placement previews.</summary>
    public RoadActionResult EvaluateDirtPlacement(GridPosition position)
    {
        if (!_island.TryGetTile(position, out var tile))
            return new RoadActionResult(RoadFailure.OutsideMap);
        if (tile.Terrain == TerrainKind.Water)
            return new RoadActionResult(RoadFailure.Water);
        if (tile.Terrain is not (TerrainKind.Grassland or TerrainKind.FertileLand))
            return new RoadActionResult(RoadFailure.RequiresFlatLand);
        if (_island.OccupiedSites.ContainsKey(position))
            return new RoadActionResult(RoadFailure.BuildingOccupied);
        if (_roads.ContainsKey(position))
            return new RoadActionResult(RoadFailure.RoadAlreadyPresent);
        return new RoadActionResult(RoadFailure.None);
    }

    /// <summary>Dirt road construction is always free (no strategic AP).</summary>
    public RoadActionResult PlaceDirt(GridPosition position)
    {
        var result = EvaluateDirtPlacement(position);
        if (result.Succeeded)
            _roads.Add(position, RoadSurface.Dirt);
        return result;
    }

    /// <summary>Pure validation for a stone-road upgrade preview.</summary>
    public RoadActionResult EvaluateStoneUpgrade(GameState state, GridPosition position)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (!_island.Contains(position))
            return new RoadActionResult(RoadFailure.OutsideMap);
        if (!_roads.TryGetValue(position, out var surface))
            return new RoadActionResult(RoadFailure.RoadMissing);
        if (surface == RoadSurface.Stone)
            return new RoadActionResult(RoadFailure.AlreadyStone);
        if (state.Get(Resource.Stone) < StoneUpgradeCost)
            return new RoadActionResult(RoadFailure.InsufficientStone);
        return new RoadActionResult(RoadFailure.None);
    }

    /// <summary>
    /// Upgrade an existing dirt road in place for 2 stone. Invalid actions do
    /// not change inventory or road tiles. No additional seasonal time is used.
    /// </summary>
    public RoadActionResult UpgradeToStone(GameState state, GridPosition position)
    {
        var validation = EvaluateStoneUpgrade(state, position);
        if (!validation.Succeeded) return validation;

        // Validate affordability a second time at the spend boundary; no partial
        // road upgrade occurs even if future mechanics modify the stockpile.
        if (!state.TrySpend(new Dictionary<Resource, int>
            {
                [Resource.Stone] = StoneUpgradeCost
            }))
            return new RoadActionResult(RoadFailure.InsufficientStone);

        _roads[position] = RoadSurface.Stone;
        return new RoadActionResult(RoadFailure.None);
    }

    /// <summary>
    /// Deterministic, four-way connected component containing a road tile.
    /// Returns an empty snapshot for a non-road tile.
    /// </summary>
    public IReadOnlyList<GridPosition> ConnectedComponent(GridPosition startingRoad)
    {
        if (!HasRoad(startingRoad))
            return Array.Empty<GridPosition>();

        var visited = new HashSet<GridPosition> { startingRoad };
        var queue = new Queue<GridPosition>();
        var ordered = new List<GridPosition>();
        queue.Enqueue(startingRoad);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var tile in _island.Neighbours(current))
            {
                var next = tile.Position;
                if (HasRoad(next) && visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        return ordered.AsReadOnly();
    }

    /// <summary>True only if both positions have roads in the same component.</summary>
    public bool AreConnected(GridPosition firstRoad, GridPosition secondRoad) =>
        HasRoad(firstRoad) && HasRoad(secondRoad) &&
        ConnectedComponent(firstRoad).Contains(secondRoad);

    /// <summary>
    /// Whether an occupied building tile is adjacent to a road segment.
    /// This is not yet a requirement for initial construction/production.
    /// </summary>
    public bool IsBuildingRoadConnected(GridPosition buildingPosition) =>
        _island.OccupiedSites.ContainsKey(buildingPosition) &&
        _island.Neighbours(buildingPosition).Any(tile => HasRoad(tile.Position));

    /// <summary>
    /// Buildings are connected if each has at least one adjacent road and
    /// those segments belong to the same four-way connected road component.
    /// Includes incomplete buildings because construction status is separate
    /// from road accessibility.
    /// </summary>
    public bool AreBuildingsConnected(GridPosition firstBuilding, GridPosition secondBuilding)
    {
        if (!_island.OccupiedSites.ContainsKey(firstBuilding) ||
            !_island.OccupiedSites.ContainsKey(secondBuilding))
            return false;

        var starts = _island.Neighbours(firstBuilding)
            .Select(tile => tile.Position).Where(HasRoad).ToArray();
        if (starts.Length == 0) return false;

        var targets = _island.Neighbours(secondBuilding)
            .Select(tile => tile.Position).Where(HasRoad).ToHashSet();
        if (targets.Count == 0) return false;

        return starts.Any(start => ConnectedComponent(start).Any(targets.Contains));
    }
}
}
