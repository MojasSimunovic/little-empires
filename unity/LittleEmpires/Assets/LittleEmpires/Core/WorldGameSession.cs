using System;
using System.Collections.Generic;
using System.Linq;

using System.Collections.ObjectModel;

namespace LittleEmpires.Core
{

/// <summary>Read-only, engine-independent rendering information for one building.</summary>
public sealed record BuildingView(
    int Index, string Id, string Name, GridPosition? Position,
    bool IsComplete, int SeasonsRemaining, bool IsUpgrading,
    int UpgradeSeasonsRemaining, int HousingCapacity,
    int WorkersRequired, int WorkPriority);

public sealed record RoadView(GridPosition Position, RoadSurface Surface, BridgeAxis Bridge = BridgeAxis.None);

/// <summary>
/// Immutable point-in-time data for the future Unity UI. Does not expose
/// modifiable simulation collections or any UnityEngine references.
/// </summary>
public sealed record WorldSnapshot(
    int Year, Season Season, int Population, int HousingCapacity,
    int Happiness, TaxPolicy TaxPolicy,
    IReadOnlyDictionary<Resource, int> Resources,
    IReadOnlyList<IslandTile> Terrain,
    IReadOnlyList<BuildingView> Buildings,
    IReadOnlyList<RoadView> Roads,
    SettlementTier Tier);

/// <summary>
/// Application-facing command boundary for a single-island game. Unity should
/// call these methods rather than reimplementing taxes, placement or trade.
/// This class is plain C#; no Unity scene or MonoBehaviour is required.
/// </summary>
public sealed class WorldGameSession
{
    private readonly TurnEngine _turnEngine = new();

    public GameState State { get; }
    public Settlement Settlement { get; }
    public IslandMap Island { get; }

    public WorldGameSession(GameState state, Settlement settlement, IslandMap island)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
        Island = island ?? throw new ArgumentNullException(nameof(island));
    }

    public static WorldGameSession NewStrategicIsland(int seed, int width = 13, int height = 13) =>
        NewStrategicIsland(IslandGenerator.Generate(seed, width, height));

    public static WorldGameSession NewStrategicIsland(IslandMap island) =>
        new(new GameState(wood: 120, stone: 60, food: 80, coins: 200),
            new Settlement(population: 8, economyMode: EconomyMode.Strategic), island);

    public PlacementResult PreviewBuilding(GridPosition position, string buildingId)
    {
        var definition = BuildingCatalog.Get(buildingId);
        if (!definition.CanBuildDirectly)
            return new PlacementResult(PlacementFailure.UpgradeOnly);
        if (!Settlement.IsBuildingUnlocked(definition))
            return new PlacementResult(PlacementFailure.RequiresSettlementTier);
        if (!Settlement.CanStartBuilding(definition))
            return new PlacementResult(PlacementFailure.CivicHallAlreadyBuilt);
        return BuildingPlacementRules.Evaluate(Island, position, definition);
    }

    public bool PlaceBuilding(GridPosition position, string buildingId) =>
        Settlement.TryStartConstruction(State, Island, position, buildingId);

    public RoadActionResult PlaceDirtRoad(GridPosition position) =>
        Island.TryGetTile(position, out var tile) && tile.Terrain == TerrainKind.River
            ? new RoadActionResult(Island.Roads.BuildPath(State, new[] { position }).Failure)
            : Island.Roads.PlaceDirt(position);

    public RoadActionResult UpgradeStoneRoad(GridPosition position) =>
        Island.Roads.UpgradeToStone(State, position);

    public RoadPathPreview PreviewRoadPath(IEnumerable<GridPosition> route, bool stone = false) =>
        Island.Roads.PreviewPath(State, route, stone);

    public RoadPathPreview BuildRoadPath(IEnumerable<GridPosition> route, bool stone = false) =>
        Island.Roads.BuildPath(State, route, stone);

    public bool UpgradeBuilding(int buildingIndex) =>
        Settlement.TryStartUpgrade(State, buildingIndex);

    public void ChangeTaxes(TaxPolicy policy) => Settlement.SetTaxPolicy(policy);

    public bool ChangeWorkPriority(int buildingIndex, int priority) =>
        Settlement.SetWorkPriority(buildingIndex, priority);

    public TradeResult Trade(
        int marketBuildingIndex, TradeDirection direction, Resource resource, int units) =>
        MarketplaceTrading.Execute(
            State, Settlement, Island, marketBuildingIndex, direction, resource, units);

    public TurnSummary EndSeason() => _turnEngine.EndTurn(State, Settlement, Island);

    public WorldSnapshot Snapshot() =>
        new(State.Year, State.Season, Settlement.Population, Settlement.HousingCapacity,
            Settlement.Happiness, Settlement.TaxPolicy,
            new ReadOnlyDictionary<Resource, int>(
                State.Resources.ToDictionary(entry => entry.Key, entry => entry.Value)),
            Array.AsReadOnly(Island.Tiles.ToArray()),
            Array.AsReadOnly(Settlement.Buildings
                .Select((building, index) => new BuildingView(
                    index, building.Definition.Id, building.Definition.Name,
                    building.Position, building.IsComplete, building.SeasonsRemaining,
                    building.IsUpgrading, building.UpgradeSeasonsRemaining,
                    building.Definition.HousingCapacity,
                    building.Definition.WorkersRequired, building.WorkPriority))
                .ToArray()),
            Array.AsReadOnly(Island.Roads.Segments
                .Select(entry => new RoadView(entry.Key, entry.Value,
                    Island.RiverCrossings.TryGetValue(entry.Key, out var axis) ? axis : BridgeAxis.None)).ToArray()),
            Settlement.Tier);
}
}
