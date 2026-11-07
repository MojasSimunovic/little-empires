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

    public static double SampleElevation(double x, double z)
    {
        var shape = SampleCoastalShape(x, z);
        // The zero crossing at sea level is unchanged. Only the immediate
        // coastal bank slopes up to the shared town grade.
        if (shape <= 4) return shape;
        if (shape < 5.5) return Lerp(4, TownElevation, Smooth((shape - 4) / 1.5));
        if (shape <= MountainBoundary) return TownElevation;
        return TownElevation + (shape - MountainBoundary) * 2;
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
