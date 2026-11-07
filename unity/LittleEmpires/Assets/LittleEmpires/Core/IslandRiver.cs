using System;
using System.Collections.Generic;

namespace LittleEmpires.Core
{
public enum BridgeAxis { None, EastWest }

/// <summary>One narrow eastern stream. Only explicitly authored level reaches support bridges.</summary>
public static class IslandRiver
{
    public const double SourceZ = 48;
    public static double CenterX(double z) => 44 + Math.Max(-1, Math.Min(2, (z - 28) / 8));
    public static bool IsRiverCell(int x, int z) => z <= SourceZ && Math.Abs(x-CenterX(z)) < .98;
    public static double WaterElevation(double x, double z) => Math.Min(9, LargeIslandGeography.SampleElevation(x,z) + .1);
    public static double Carve(double x, double z, double elevation)
    {
        if (z > SourceZ + .45) return elevation;
        var distance = Math.Abs(x - CenterX(z));
        var blend = Math.Max(0, Math.Min(1, (.48 - distance) / .22));
        blend *= Math.Max(0, Math.Min(1, (SourceZ + .45 - z) / .45));
        blend = blend * blend * (3 - 2 * blend);
        return elevation + (Math.Min(elevation, WaterElevation(x,z) - 1.5) - elevation) * blend;
    }
    public static IReadOnlyDictionary<GridPosition, BridgeAxis> Crossings() =>
        new Dictionary<GridPosition, BridgeAxis>
        {
            [new GridPosition(44,28)] = BridgeAxis.EastWest,
            [new GridPosition(45,36)] = BridgeAxis.EastWest,
            [new GridPosition(46,44)] = BridgeAxis.EastWest
        };
}
}
