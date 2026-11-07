using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

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

        if (width == 64 && height == 64)
        {
            var authoredTiles = new List<IslandTile>(width * height);
            for (var z = 0; z < height; z++)
                for (var x = 0; x < width; x++)
                    authoredTiles.Add(new IslandTile(new GridPosition(x, z),
                        LargeIslandGeography.Classify(x, z)));
            return new IslandMap(width, height, authoredTiles, IslandRiver.Crossings());
        }

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
                    (width < 32 || height < 32) && distance >= 0.84 + random.NextDouble() * 0.25)
                {
                    terrain[x, z] = TerrainKind.Water;
                    continue;
                }

                if (width >= 32 && height >= 32)
                {
                    var angle = Math.Atan2(dz, dx);
                    var coast = .78 + .09 * Math.Sin(angle * 3 + seed) + .06 * Math.Cos(angle * 5);
                    terrain[x, z] = distance > coast ? TerrainKind.Water :
                        (dx-.38)*(dx-.38)/.075+(dz-.38)*(dz-.38)/.10 < 1 ? TerrainKind.Mountain :
                        (dx+.46)*(dx+.46)/.06+(dz-.05)*(dz-.05)/.20 < 1 || (dx-.48)*(dx-.48)/.05+(dz+.30)*(dz+.30)/.09 < 1 ? TerrainKind.Forest :
                        dx*dx/.10+(dz+.43)*(dz+.43)/.065 < 1 ? TerrainKind.FertileLand : TerrainKind.Grassland;
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
        // Preserve a flat approach from the settlement to the guaranteed
        // mountain. Future road networks can use this corridor.
        if (width < 32 || height < 32) {
        terrain[centerX + 2, centerZ - 1] = TerrainKind.Mountain;
        terrain[centerX + 1, centerZ] = TerrainKind.Grassland;
        terrain[centerX + 1, centerZ - 1] = TerrainKind.Grassland;
        terrain[centerX - 2, centerZ] = TerrainKind.Forest;
        terrain[centerX - 1, centerZ] = TerrainKind.Grassland;
        terrain[centerX, centerZ + 1] = TerrainKind.FertileLand;
        terrain[centerX, centerZ] = TerrainKind.Grassland;

        }
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
}
