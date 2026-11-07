using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{
public sealed record RoadPathPreview(IReadOnlyList<GridPosition> Segments, int CostWood,
    int CostStone, RoadFailure Failure, GridPosition? FailureAt)
{
    public bool Succeeded => Failure == RoadFailure.None;
}

/// <summary>Deterministic orthogonal routing; all placement rules remain in the road network.</summary>
public static class RoadPaths
{
    public static IReadOnlyList<GridPosition> Between(GridPosition start, GridPosition end, bool xFirst = true)
    {
        var result = new List<GridPosition> { start };
        var x = start.X;
        var z = start.Z;
        for (var axis = 0; axis < 2; axis++)
        {
            if ((axis == 0) == xFirst)
                while (x != end.X) { x += Math.Sign(end.X - x); result.Add(new GridPosition(x, z)); }
            else
                while (z != end.Z) { z += Math.Sign(end.Z - z); result.Add(new GridPosition(x, z)); }
        }
        return result.AsReadOnly();
    }
}

public sealed partial class IslandRoadNetwork
{
    public RoadPathPreview PreviewPath(GameState state, IEnumerable<GridPosition> route, bool stone = false)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (route == null) throw new ArgumentNullException(nameof(route));
        var ordered = route.ToArray();
        var points = Array.AsReadOnly(ordered.Distinct().ToArray());
        var cost = 0;
        RoadPathPreview Fail(RoadFailure reason, GridPosition? at) => new(points, 0, cost, reason, at);
        if (ordered.Length == 0) return Fail(RoadFailure.InvalidPath, null);
        for (var i = 1; i < ordered.Length; i++)
            if (Math.Abs(ordered[i].X - ordered[i-1].X) + Math.Abs(ordered[i].Z - ordered[i-1].Z) > 1)
                return Fail(RoadFailure.InvalidPath, ordered[i]);
        foreach (var point in points)
        {
            if (!_island.Contains(point)) return Fail(RoadFailure.OutsideMap, point);
            if (stone)
            {
                if (!_roads.TryGetValue(point, out var surface)) return Fail(RoadFailure.RoadMissing, point);
                if (surface != RoadSurface.Stone) cost = checked(cost + StoneUpgradeCost);
            }
            else if (!HasRoad(point))
            {
                var validation = EvaluateDirtPlacement(point);
                if (!validation.Succeeded) return Fail(validation.Failure, point);
            }
        }
        return Fail(state.Get(Resource.Stone) < cost ? RoadFailure.InsufficientStone : RoadFailure.None, null);
    }

    public RoadPathPreview BuildPath(GameState state, IEnumerable<GridPosition> route, bool stone = false)
    {
        var preview = PreviewPath(state, route, stone);
        if (!preview.Succeeded) return preview;
        if (!state.TrySpend(new Dictionary<Resource, int> { [Resource.Stone] = preview.CostStone, [Resource.Wood] = preview.CostWood }))
            return preview with { Failure = RoadFailure.InsufficientStone };
        foreach (var point in preview.Segments)
            if (stone || !HasRoad(point)) _roads[point] = stone ? RoadSurface.Stone : RoadSurface.Dirt;
        return preview;
    }
}
}
