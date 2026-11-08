using System;

namespace LittleEmpires.Core
{
/// <summary>
/// Editable control map for the fixed 64x64 island. Rows run south to north.
/// Elevation digits describe an inlet, a broad town basin, and a northern ridge.
/// Both gameplay classifications and Unity's heightmap sample this same source.
/// </summary>
public static class LargeIslandGeography
{
    // Terrain's world origin is -8, so this puts the town floor at world Y = 2.
    public const double TownElevation = 10;
    private const double MountainBoundary = 20;
    private static readonly string[] Elevation =
    {
        "00000000000000000",
        "00012000000021000",
        "00123200000232100",
        "01234300000343210",
        "02344420002444320",
        "02344432234444320",
        "02344443344444320",
        "02344444344444320",
        "01344444344444320",
        "00134444344444320",
        "01344444444444310",
        "02344444444444320",
        "02344444444555320",
        "01234444445676420",
        "00123444456787510",
        "00012344456876100",
        "00000012100010000"
    };

    // F denotes woodland, A denotes farmland. The center remains open for a city.
    private static readonly string[] Biomes =
    {
        ".................",
        ".................",
        ".................",
        "...F.............",
        "...FF......AA....",
        "...FFF.....AAAA..",
        "..FFFF......AAA..",
        "..FFFFF.....AA...",
        "..FFFFF....AAA...",
        "...FFFF...AAAA...",
        "....FFF...AAAA...",
        ".....FF...AAA....",
        "......F..........",
        ".................",
        ".................",
        ".................",
        "................."
    };

    // Curated one-plot-wide level building shelves. All other terrain remains
    // the original authored island: this is not a global flattening pass.
    // Locations are gameplay coordinates, not Unity world coordinates.
    private static readonly GridPosition[] FisheryShelves =
    {
        new GridPosition(47, 2), new GridPosition(18, 7),
        new GridPosition(44, 9), new GridPosition(26, 14),
        new GridPosition(30, 18), new GridPosition(3, 14),
        new GridPosition(4, 25), new GridPosition(7, 36),
        new GridPosition(3, 41), new GridPosition(3, 45),
        new GridPosition(61, 15), new GridPosition(60, 28),
        new GridPosition(61, 32), new GridPosition(61, 43),
        new GridPosition(61, 49), new GridPosition(13, 60),
        new GridPosition(16, 61)
    };

    // Mountain-foot ledges remain Mountain tiles for quarry placement, with
    // adjacent flat Grassland/FertileLand tiles for road access.
    private static readonly GridPosition[] QuarryLedges =
    {
        new GridPosition(35, 56), new GridPosition(35, 57),
        new GridPosition(38, 53), new GridPosition(41, 52),
        new GridPosition(45, 49), new GridPosition(47, 49),
        new GridPosition(48, 49), new GridPosition(49, 49),
        new GridPosition(50, 49), new GridPosition(51, 48),
        new GridPosition(52, 48), new GridPosition(52, 49),
        new GridPosition(53, 50), new GridPosition(54, 56)
    };

    public const double MaximumBuildingRelief = .25;

    public static double SampleElevation(double x, double z)
    {
        var shape = SampleCoastalShape(x, z);
        // Widen the sea-to-city ramp from 1.5 to 4 coastal-mask units.
        // The waterline (shape == 4), land silhouette, and inland level are preserved.
        var elevation = CoastalElevation(shape);

        // Flatten a full 6-unit logical plot beneath each approved building shelf.
        // Soft transitions lie OUTSIDE its footprint, so foundations never tilt.
        foreach (var site in FisheryShelves)
        {
            var distance = Math.Max(Math.Abs(x - site.X), Math.Abs(z - site.Z));
            var influence = PadInfluence(distance);
            if (influence > 0)
                elevation = Lerp(elevation,
                    CoastalElevation(SampleCoastalShape(site.X, site.Z)), influence);
        }
        foreach (var site in QuarryLedges)
        {
            var distance = Math.Max(Math.Abs(x - site.X), Math.Abs(z - site.Z));
            var influence = PadInfluence(distance);
            if (influence > 0)
                elevation = Lerp(elevation, TownElevation, influence);
        }
        // Do not turn ocean cells into dry land near a level shoreline pad.
        return shape <= 4 ? Math.Min(shape, elevation) : elevation;
    }

    // Samples the exact height geometry Unity renders, including river carving.
    // Rejecting steep building cells is safer than accepting them and visually
    // floating the building from a single center-height sample.
    public static double BuildingFootprintRelief(GridPosition plot)
    {
        var (min, max) = BuildingHeightRange(plot);
        return max - min;
    }

    public static (double Min, double Max) BuildingHeightRange(GridPosition plot)
    {
        double min = double.MaxValue, max = double.MinValue;
        for (var z = 0; z < 7; z++)
            for (var x = 0; x < 7; x++)
            {
                var gx = plot.X - .49 + x * .98 / 6;
                var gz = plot.Z - .49 + z * .98 / 6;
                var height = IslandRiver.Carve(gx, gz, SampleElevation(gx, gz));
                min = Math.Min(min, height);
                max = Math.Max(max, height);
            }
        return (min, max);
    }

    private static double CoastalElevation(double shape)
    {
        if (shape <= 4) return shape;
        if (shape < 8)
            return Lerp(4, TownElevation, Smooth((shape - 4) / 4));
        if (shape <= MountainBoundary) return TownElevation;
        return TownElevation + (shape - MountainBoundary) * 2;
    }

    private static double PadInfluence(double distance)
    {
        if (distance <= .52) return 1;
        if (distance >= 1.28) return 0;
        return 1 - Smooth((distance - .52) / .76);
    }

    public static double SampleCoastalShape(double x, double z)
    {
        var warpedX = x + Math.Sin(z * .22 + x * .043) * 1.1 + Math.Sin(z * .47) * .6;
        var warpedZ = z + Math.Cos(x * .25 - z * .052) + Math.Sin(x * .33) * .5;
        var gx = Math.Max(0, Math.Min(16, warpedX / 4));
        var gz = Math.Max(0, Math.Min(16, warpedZ / 4));
        var ix = Math.Min(15, (int)gx);
        var iz = Math.Min(15, (int)gz);
        var tx = Smooth(gx - ix);
        var tz = Smooth(gz - iz);
        var south = Lerp(Elevation[iz][ix] - '0', Elevation[iz][ix + 1] - '0', tx);
        var north = Lerp(Elevation[iz + 1][ix] - '0', Elevation[iz + 1][ix + 1] - '0', tx);
        var baseHeight = Lerp(south, north, tz) * 4;
        var rolling = (Math.Sin(x * .29 + 1.7) * Math.Cos(z * .20 - .8) +
                       Math.Sin(x * .13 - z * .11) * .7) * .65;
        return Math.Max(0, baseHeight + rolling * Math.Min(1, baseHeight / 8));
    }

    public static TerrainKind Classify(int x, int z)
    {
        var elevation = SampleElevation(x, z);
        if (elevation <= 4) return TerrainKind.Water;
        if (IslandRiver.IsRiverCell(x,z)) return TerrainKind.River;
        if (SampleCoastalShape(x, z) > MountainBoundary) return TerrainKind.Mountain;
        var biome = Biomes[Math.Min(16, z / 4)][Math.Min(16, x / 4)];
        if (biome == 'F') return TerrainKind.Forest;
        if (biome == 'A') return TerrainKind.FertileLand;
        return TerrainKind.Grassland;
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
}
