using System;
using System.Collections.Generic;
using System.Linq;

using System.Collections.ObjectModel;

namespace LittleEmpires.Core
{

/// <summary>Logical island coordinates. Unity converts these to world positions.</summary>
public readonly struct GridPosition : IEquatable<GridPosition>
{
    public int X { get; }
    public int Z { get; }
    public GridPosition(int x, int z) { X = x; Z = z; }
    // The anchor is the minimum X/Z cell.
    public IEnumerable<GridPosition> Rectangle(int width, int height)
    {
        for (var z = 0; z < height; z++)
            for (var x = 0; x < width; x++)
                yield return new GridPosition(X + x, Z + z);
    }

    public bool Equals(GridPosition other) => X == other.X && Z == other.Z;
    public override bool Equals(object? obj) => obj is GridPosition other && Equals(other);
    public override int GetHashCode() { unchecked { return (X * 397) ^ Z; } }
    public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
    public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
}

public enum TerrainKind
{
    Water,
    Grassland,
    FertileLand,
    Forest,
    Mountain,
    River
}

public readonly struct IslandTile
{
    public GridPosition Position { get; }
    public TerrainKind Terrain { get; }
    public IslandTile(GridPosition position, TerrainKind terrain)
    {
        Position = position;
        Terrain = terrain;
    }
}

/// <summary>
/// Rectangular terrain grid with a separate occupancy layer. Terrain is immutable
/// after creation; road and structure layers can evolve without changing terrain.
/// </summary>
public sealed class IslandMap
{
    private readonly IslandTile[,] _tiles;
    private readonly Dictionary<GridPosition, Building> _occupants = new();
    private readonly IReadOnlyDictionary<GridPosition, Building> _readOnlyOccupants;
    private readonly IReadOnlyList<IslandTile> _allTiles;

    private readonly Func<GridPosition, (double Min, double Max)>? _heightRange;

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<IslandTile> Tiles => _allTiles;
    public IReadOnlyDictionary<GridPosition, Building> OccupiedSites => _readOnlyOccupants;
    public IslandRoadNetwork Roads { get; }
    public IReadOnlyDictionary<GridPosition, BridgeAxis> RiverCrossings { get; }

    public IslandMap(int width, int height, IEnumerable<IslandTile> tiles,
        IReadOnlyDictionary<GridPosition, BridgeAxis>? riverCrossings = null,
        Func<GridPosition, (double Min, double Max)>? heightRange = null)
    {
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
        if (tiles == null) throw new ArgumentNullException(nameof(tiles));
        _heightRange = heightRange;
        Width = width;
        Height = height;
        _tiles = new IslandTile[width, height];
        var seen = new bool[width, height];
        var collected = new List<IslandTile>();

        foreach (var tile in tiles)
        {
            var position = tile.Position;
            if (!Contains(position))
                throw new ArgumentException("Tile position is outside the island grid.", nameof(tiles));
            if (seen[position.X, position.Z])
                throw new ArgumentException("Duplicate tile position.", nameof(tiles));
            if (!Enum.IsDefined(typeof(TerrainKind), tile.Terrain))
                throw new ArgumentException("Unknown terrain type.", nameof(tiles));

            seen[position.X, position.Z] = true;
            _tiles[position.X, position.Z] = tile;
            collected.Add(tile);
        }

        if (collected.Count != (long)width * height)
            throw new ArgumentException("Every grid position requires exactly one tile.", nameof(tiles));

        _allTiles = collected.AsReadOnly();
        var crossings = riverCrossings == null ? new Dictionary<GridPosition, BridgeAxis>() :
            riverCrossings.ToDictionary(p => p.Key, p => p.Value);
        foreach (var crossing in crossings)
            if (!Contains(crossing.Key) || GetTile(crossing.Key).Terrain != TerrainKind.River || crossing.Value != BridgeAxis.EastWest)
                throw new ArgumentException("Crossings must be east-west spans over river tiles.", nameof(riverCrossings));
        RiverCrossings = new ReadOnlyDictionary<GridPosition, BridgeAxis>(crossings);
        _readOnlyOccupants = new ReadOnlyDictionary<GridPosition, Building>(_occupants);
        Roads = new IslandRoadNetwork(this);
    }

    public double BuildingFootprintRelief(GridPosition position, int width = 1, int height = 1)
    {
        double low = double.PositiveInfinity, high = double.NegativeInfinity;
        foreach (var cell in position.Rectangle(width, height))
        {
            var range = _heightRange?.Invoke(cell) ?? (Width == 64 && Height == 64
                ? LargeIslandGeography.BuildingHeightRange(cell) : (0d, 0d));
            low = Math.Min(low, range.Item1);
            high = Math.Max(high, range.Item2);
        }
        return high - low;
    }

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width &&
        position.Z >= 0 && position.Z < Height;

    public bool TryGetTile(GridPosition position, out IslandTile tile)
    {
        if (!Contains(position))
        {
            tile = default;
            return false;
        }

        tile = _tiles[position.X, position.Z];
        return true;
    }

    public IslandTile GetTile(GridPosition position) =>
        TryGetTile(position, out var tile)
            ? tile
            : throw new ArgumentOutOfRangeException(nameof(position));

    public IEnumerable<IslandTile> Neighbours(GridPosition position)
    {
        if (!Contains(position)) yield break;
        var offsets = new[] { (X: 0, Z: -1), (X: 1, Z: 0),
                              (X: 0, Z: 1), (X: -1, Z: 0) };
        foreach (var (x, z) in offsets)
        {
            var next = new GridPosition(position.X + x, position.Z + z);
            if (TryGetTile(next, out var tile)) yield return tile;
        }
    }

    public bool IsShoreline(GridPosition position) =>
        TryGetTile(position, out var tile) &&
        tile.Terrain != TerrainKind.Water && tile.Terrain != TerrainKind.River &&
        Neighbours(position).Any(neighbour => neighbour.Terrain == TerrainKind.Water);

    public bool TryGetBuilding(GridPosition position, out Building? building) =>
        _occupants.TryGetValue(position, out building);

    internal void Occupy(GridPosition position, Building building)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        foreach (var cell in position.Rectangle(building.Definition.Width, building.Definition.Height))
            _occupants.Add(cell, building);
    }
    internal void Release(Building building)
    {
        if (!building.Position.HasValue) return;
        var cells = building.Position.Value.Rectangle(building.Definition.Width, building.Definition.Height).ToArray();
        if (cells.Any(cell => !_occupants.TryGetValue(cell, out var occupant) || !ReferenceEquals(occupant, building)))
            throw new InvalidOperationException("The building footprint does not match island occupancy.");
        foreach (var cell in cells)
            _occupants.Remove(cell);
    }
}
}
