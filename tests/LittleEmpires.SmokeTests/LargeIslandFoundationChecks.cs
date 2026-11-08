using System;
using System.Linq;
using LittleEmpires.Core;

/// <summary>Regression checks for real 6-unit building footprints on the authored 64x64 island.</summary>
public static class LargeIslandFoundationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var island = IslandGenerator.Generate(seed: 42, width: 64, height: 64);
        var fishery = BuildingCatalog.Get(BuildingCatalog.FisheryId);
        var quarry = BuildingCatalog.Get(BuildingCatalog.QuarryId);

        var fisherySites = island.Tiles.Where(tile =>
            BuildingPlacementRules.Evaluate(island, tile.Position, fishery).Allowed).ToArray();
        var quarrySites = island.Tiles.Where(tile =>
            BuildingPlacementRules.Evaluate(island, tile.Position, quarry).Allowed).ToArray();

        check(fisherySites.Length >= 12, "T-1: at least twelve level, buildable shoreline sites");
        check(quarrySites.Length >= 10, "T-2: at least ten level, buildable quarry ledges");

        check(fisherySites.All(tile =>
                island.IsShoreline(tile.Position) &&
                LargeIslandGeography.BuildingFootprintRelief(tile.Position) <=
                    LargeIslandGeography.MaximumBuildingRelief),
            "T-1: all Fishery-eligible footprints stay level within 0.25 terrain units");
        check(quarrySites.All(tile =>
                tile.Terrain == TerrainKind.Mountain &&
                LargeIslandGeography.BuildingFootprintRelief(tile.Position) <=
                    LargeIslandGeography.MaximumBuildingRelief),
            "T-2: all Quarry-eligible footprints stay level within 0.25 terrain units");

        // Sample different shores rather than merely the same contiguous shoreline shelf.
        var coastalShowcase = new[]
        {
            new GridPosition(47, 2),   // south
            new GridPosition(3, 14),   // west
            new GridPosition(26, 14),  // bay
            new GridPosition(60, 28),  // east
            new GridPosition(13, 60)   // north
        };
        check(coastalShowcase.All(p => BuildingPlacementRules.Evaluate(island, p, fishery).Allowed),
            "T-1: five geographically separate coastal Fishery foundation shelves are buildable");

        var accessibleLedges = quarrySites.Where(tile =>
            island.Neighbours(tile.Position).Any(neighbor =>
                island.Roads.EvaluateDirtPlacement(neighbor.Position).Succeeded)).ToArray();
        check(accessibleLedges.Length >= 10,
            "T-2: ten level mountain-foot ledges have an adjacent road-buildable tile");

        // Creating plots should be financially possible and should not affect other terrain types.
        var fishingSession = WorldGameSession.NewStrategicIsland(42, 64, 64);
        check(coastalShowcase.All(p => fishingSession.PlaceBuilding(p, BuildingCatalog.FisheryId)),
            "T-1: construction starts at five distinct shoreline shelves");
        var quarrySession = WorldGameSession.NewStrategicIsland(42, 64, 64);
        var quarryShowcase = new[]
        {
            new GridPosition(35, 56),
            new GridPosition(49, 49),
            new GridPosition(53, 50)
        };
        check(quarryShowcase.All(p => quarrySession.PlaceBuilding(p, BuildingCatalog.QuarryId)),
            "T-2: construction starts on three different quarry ledges");

        var unevenCoast = island.Tiles.FirstOrDefault(tile =>
            island.IsShoreline(tile.Position) &&
            tile.Terrain is TerrainKind.Grassland or TerrainKind.FertileLand &&
            LargeIslandGeography.BuildingFootprintRelief(tile.Position) >
                LargeIslandGeography.MaximumBuildingRelief);
        check(BuildingPlacementRules.Evaluate(island, unevenCoast.Position, fishery).Failure ==
                PlacementFailure.RequiresLevelSite,
            "T-1: undeveloped sloped shoreline cannot accept a floating Fishery");

        var unevenMountain = island.Tiles.FirstOrDefault(tile =>
            tile.Terrain == TerrainKind.Mountain &&
            LargeIslandGeography.BuildingFootprintRelief(tile.Position) >
                LargeIslandGeography.MaximumBuildingRelief);
        check(BuildingPlacementRules.Evaluate(island, unevenMountain.Position, quarry).Failure ==
                PlacementFailure.RequiresLevelSite,
            "T-2: steep mountain cells do not accept floating Quarries");

        check(island.GetTile(new GridPosition(32, 32)).Terrain == TerrainKind.Grassland &&
              LargeIslandGeography.BuildingFootprintRelief(new GridPosition(32, 32)) < .01,
            "Flat central city-building basin remains undisturbed");

        check(island.Tiles.All(tile =>
                LargeIslandGeography.SampleCoastalShape(tile.Position.X, tile.Position.Z) > 4 ||
                tile.Terrain == TerrainKind.Water),
            "Original sea/land silhouette and water classification are preserved");
    }
}
