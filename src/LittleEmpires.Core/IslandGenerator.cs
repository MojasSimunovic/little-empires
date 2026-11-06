namespace LittleEmpires.Core;

/// <summary>
/// Deterministic prototype island generation. Every supported island contains
/// at least one mountain quarry site, forest-edge logging site, fertile farm site,
/// and usable shoreline. The guarantees are rules, not visual decoration.
/// </summary>
public static class IslandGenerator
{
    public static IslandMap Generate(int seed, int width = 13, int height = 13)
    {
        if (width is < 9 or > 64) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 9 or > 64) throw new ArgumentOutOfRangeException(nameof(height));

        var random = new Random(seed);
        var terrain = new TerrainKind[width, height];
        var centerX = width / 2;
        var centerZ = height / 2;
        var radiusX = (width - 2) / 2d;
        var radiusZ = (height - 2) / 2d;

        for (var z = 0; z < height; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = (x - centerX) / radiusX;
                var dz = (z - centerZ) / radiusZ;
                var distance = dx * dx + dz * dz;

                if (x == 0 || z == 0 || x == width - 1 || z == height - 1 ||
                    distance >= 0.84 + random.NextDouble() * 0.25)
                {
                    terrain[x, z] = TerrainKind.Water;
                    continue;
                }

                var roll = random.NextDouble();
                terrain[x, z] = roll switch
                {
                    < 0.08 => TerrainKind.Forest,
                    < 0.12 => TerrainKind.Mountain,
                    < 0.25 => TerrainKind.FertileLand,
                    _ => TerrainKind.Grassland
                };
            }
        }

        // Guaranteed opportunities, independent of the random roll.
        terrain[centerX + 2, centerZ - 1] = TerrainKind.Mountain;
        terrain[centerX - 2, centerZ] = TerrainKind.Forest;
        terrain[centerX - 1, centerZ] = TerrainKind.Grassland;
        terrain[centerX, centerZ + 1] = TerrainKind.FertileLand;
        terrain[centerX, centerZ] = TerrainKind.Grassland;

        // Make at least one shoreline location suitable for a Fishery.
        var hasUsableShoreline = false;
        GridPosition? firstShoreline = null;
        for (var z = 1; z < height - 1; z++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                if (terrain[x, z] == TerrainKind.Water ||
                    !(terrain[x - 1, z] == TerrainKind.Water ||
                      terrain[x + 1, z] == TerrainKind.Water ||
                      terrain[x, z - 1] == TerrainKind.Water ||
                      terrain[x, z + 1] == TerrainKind.Water))
                    continue;

                firstShoreline ??= new GridPosition(x, z);
                if (terrain[x, z] is TerrainKind.Grassland or TerrainKind.FertileLand)
                    hasUsableShoreline = true;
            }
        }

        if (firstShoreline is null)
            throw new InvalidOperationException("Island generator produced no shoreline.");
        if (!hasUsableShoreline)
            terrain[firstShoreline.Value.X, firstShoreline.Value.Z] = TerrainKind.Grassland;

        var tiles = new List<IslandTile>(width * height);
        for (var z = 0; z < height; z++)
            for (var x = 0; x < width; x++)
                tiles.Add(new IslandTile(new GridPosition(x, z), terrain[x, z]));

        return new IslandMap(width, height, tiles);
    }
}
