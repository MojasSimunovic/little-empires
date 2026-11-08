using System;
using System.Collections.Generic;

namespace LittleEmpires.Core
{
public enum BridgeAxis { None, EastWest }

/// <summary>One narrow eastern stream. Only explicitly authored level reaches support bridges.</summary>
public static class IslandRiver
{
    public const double SourceZ = 48;
    // The river starts at the bay mouth; extending it south creates invisible land restrictions.
    public const int MouthZ = 13;
    public static double CenterX(double z) => 44 + Math.Max(-1, Math.Min(2, (z - 28) / 8));
    // Exactly one gameplay river plot per row instead of a two-cell band around a narrow mesh.
    // Midpoints round up consistently, unlike Math.Round's midpoint-to-even behavior.
    public static int RiverColumn(int z) => (int)Math.Floor(CenterX(z) + .5);

    // Interpolate the drawn channel between the same plot centers used by the C# mask.
    public static double VisualCenterX(double z)
    {
        var row = Math.Max(MouthZ, Math.Min((int)SourceZ - 1, (int)Math.Floor(z)));
        var t = Math.Max(0, Math.Min(1, z - row));
        return RiverColumn(row) + (RiverColumn(row + 1) - RiverColumn(row)) * t;
    }

    public static bool IsRiverCell(int x, int z) =>
        z >= MouthZ && z <= SourceZ && x == RiverColumn(z);
    public static double WaterElevation(double x, double z) => Math.Min(9, LargeIslandGeography.SampleElevation(x,z) + .1);
    public static double Carve(double x, double z, double elevation)
    {
        if (z < MouthZ - .5 || z > SourceZ + .45) return elevation;
        var distance = Math.Abs(x - VisualCenterX(z));
        var blend = Math.Max(0, Math.Min(1, (.65 - distance) / .18));
        blend *= Math.Max(0, Math.Min(1, (z - (MouthZ - .5)) / .5));
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
