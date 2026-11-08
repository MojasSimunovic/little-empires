using System;
using System.Linq;
using LittleEmpires.Core;

public static class BuildingFootprintTests
{
    public static void Run(Action<bool, string> check)
    {
        var anchor = new GridPosition(2, 2);
        var far = new GridPosition(3, 3);
        var hall = BuildingCatalog.Get(BuildingCatalog.VillageHallId);
        var town = BuildingCatalog.Get(BuildingCatalog.TownHallId);
        var cells = anchor.Rectangle(2, 2).ToArray();
        IslandMap Map(TerrainKind corner = TerrainKind.Grassland, double rise = 0) =>
            new(7, 7, new GridPosition(0, 0).Rectangle(7, 7)
                .Select(p => new IslandTile(p, p == far ? corner : TerrainKind.Grassland)),
                heightRange: p => p == far ? (rise, rise) : (0d, 0d));

        check(hall.Width == 2 && hall.Height == 2 && town.Width == 2 && town.Height == 2 &&
              BuildingCatalog.Get(BuildingCatalog.CottageId).Width == 1, "Only civic footprints are 2x2");
        check(BuildingPlacementRules.Evaluate(Map(), new GridPosition(6, 6), hall).Failure == PlacementFailure.OutsideMap,
            "Hall bounds include the unclicked three tiles");
        check(BuildingPlacementRules.Evaluate(Map(), new GridPosition(5, 5), hall with { Width = 3 }).Failure ==
              PlacementFailure.OutsideMap, "Rectangular footprints check width independently of height");
        check(BuildingPlacementRules.Evaluate(Map(TerrainKind.Water), anchor, hall).Failure == PlacementFailure.Water,
            "A submerged corner rejects the complete Hall footprint");
        check(BuildingPlacementRules.Evaluate(Map(TerrainKind.Forest), anchor, hall).Failure == PlacementFailure.RequiresFlatLand,
            "An unsuitable corner rejects the complete Hall footprint");
        check(BuildingPlacementRules.Evaluate(Map(rise: .4), anchor, hall).Failure == PlacementFailure.RequiresLevelSite,
            "Separately flat cells with different elevations reject the complete foundation");

        var island = Map();
        var state = new GameState(wood: 500, stone: 500, coins: 900, food: 600);
        var game = new WorldGameSession(state, new Settlement(population: 50, economyMode: EconomyMode.Strategic), island);
        var wood = state.Get(Resource.Wood);
        check(game.PlaceDirtRoad(far).Succeeded &&
              game.PreviewBuilding(anchor, hall.Id).Failure == PlacementFailure.RoadPresent &&
              !game.PlaceBuilding(anchor, hall.Id) && island.OccupiedSites.Count == 0 && state.Get(Resource.Wood) == wood,
            "A road on the far corner prevents occupancy and charging");

        island = Map();
        game = new WorldGameSession(state, new Settlement(population: 50, economyMode: EconomyMode.Strategic), island);
        check(game.PlaceBuilding(far, BuildingCatalog.CottageId) &&
              game.PreviewBuilding(anchor, hall.Id).Failure == PlacementFailure.AlreadyOccupied,
            "A one-cell overlap blocks Hall placement");
        wood = state.Get(Resource.Wood);
        check(!game.PlaceBuilding(anchor, hall.Id) && island.OccupiedSites.Count == 1 && state.Get(Resource.Wood) == wood,
            "Failed overlap leaves both occupancy and treasury intact");

        island = Map();
        game = new WorldGameSession(state, new Settlement(population: 50, economyMode: EconomyMode.Strategic), island);
        wood = state.Get(Resource.Wood);
        var stone = state.Get(Resource.Stone);
        var coins = state.Get(Resource.Coins);
        check(game.PlaceBuilding(anchor, hall.Id) && island.OccupiedSites.Count == 4 &&
              state.Get(Resource.Wood) == wood - hall.Cost[Resource.Wood] &&
              state.Get(Resource.Stone) == stone - hall.Cost[Resource.Stone] &&
              state.Get(Resource.Coins) == coins - hall.Cost[Resource.Coins],
            "A valid Hall occupies four tiles and charges exactly once");
        var building = game.Settlement.Buildings.Single();
        check(cells.All(p => island.TryGetBuilding(p, out var occupant) && ReferenceEquals(building, occupant)) &&
              game.Snapshot().Buildings[0].OccupiedPlots.SequenceEqual(cells),
            "All four coordinates resolve to the same building identity");
        check(cells.All(p => !game.PlaceDirtRoad(p).Succeeded), "No roads through any Hall footprint tile");
        var perimeter = new GridPosition(4, 3);
        var next = new GridPosition(5, 3);
        check(game.PlaceDirtRoad(perimeter).Succeeded && game.PlaceDirtRoad(next).Succeeded &&
              game.PlaceBuilding(new GridPosition(6, 3), BuildingCatalog.CottageId) &&
              island.Roads.IsBuildingRoadConnected(anchor) &&
              island.Roads.AreBuildingsConnected(far, new GridPosition(6, 3)),
            "Roads connect at the far perimeter and from any occupied cell");
        check(!game.PlaceBuilding(new GridPosition(0, 0), hall.Id), "The Hall remains unique");
        game.EndSeason();
        game.ChangeTaxes(TaxPolicy.High);
        check(game.UpgradeBuilding(0), "The completed spatial Hall can upgrade");
        game.EndSeason();
        game.EndSeason();
        check(building.Definition.Id == town.Id && building.Position == anchor &&
              cells.All(p => ReferenceEquals(island.OccupiedSites[p], building)) &&
              game.Snapshot().TaxPolicy == TaxPolicy.High, "Town Hall retains identity, all four cells, anchor, and taxes");
    }
}
