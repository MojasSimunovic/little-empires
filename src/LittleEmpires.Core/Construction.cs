using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

/// <summary>Expansion tiers. Future tiers add an enum member and unlock rule.</summary>
public enum SettlementTier { Village = 1, Town = 2 }

public sealed record BuildingDefinition(
    string Id,
    string Name,
    IReadOnlyDictionary<Resource, int> Cost,
    int ConstructionSeasons,
    IReadOnlyDictionary<Resource, int> ProductionPerSeason,
    int HousingCapacity = 0,
    bool CanBuildDirectly = true,
    PlacementRequirement SiteRequirement = PlacementRequirement.FlatLand,
    int WorkersRequired = 0,
    int UpkeepCoinsPerSeason = 0,
    SettlementTier RequiredTier = SettlementTier.Village,
    int Width = 1,
    int Height = 1)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("Building ID is required.");
        if (Width < 1) throw new ArgumentOutOfRangeException(nameof(Width));
        if (Height < 1) throw new ArgumentOutOfRangeException(nameof(Height));
        if (ConstructionSeasons < 1) throw new ArgumentOutOfRangeException(nameof(ConstructionSeasons));
        if (HousingCapacity < 0) throw new ArgumentOutOfRangeException(nameof(HousingCapacity));
        if (WorkersRequired < 0) throw new ArgumentOutOfRangeException(nameof(WorkersRequired));
        if (UpkeepCoinsPerSeason < 0) throw new ArgumentOutOfRangeException(nameof(UpkeepCoinsPerSeason));
        if (!Enum.IsDefined(typeof(PlacementRequirement), SiteRequirement)) throw new ArgumentOutOfRangeException(nameof(SiteRequirement));
        if (!Enum.IsDefined(typeof(SettlementTier), RequiredTier)) throw new ArgumentOutOfRangeException(nameof(RequiredTier));
        if (Cost.Values.Any(v => v < 0) || ProductionPerSeason.Values.Any(v => v < 0))
            throw new ArgumentException("Building amounts cannot be negative.");
    }
}

public sealed class Building
{
    public int InstanceId { get; }
    public BuildingDefinition Definition { get; private set; }
    public GridPosition? Position { get; }
    public int WorkPriority { get; private set; }
    public int UnitsTradedThisSeason { get; private set; }
    public int SeasonsRemaining { get; private set; }
    public bool IsComplete => SeasonsRemaining == 0;
    public int UpgradeSeasonsRemaining { get; private set; }
    public string? PendingUpgradeToId => _pendingUpgrade?.ToBuildingId;
    public bool IsUpgrading => _pendingUpgrade is not null;
    private BuildingUpgradeDefinition? _pendingUpgrade;

    internal Building(int instanceId, BuildingDefinition definition, GridPosition? position = null)
    {
        InstanceId = instanceId;
        Definition = definition;
        Position = position;
        SeasonsRemaining = definition.ConstructionSeasons;
    }

    internal void ProgressConstruction()
    {
        if (SeasonsRemaining > 0) SeasonsRemaining--;
    }

    internal void SetWorkPriority(int priority) => WorkPriority = priority;

    internal void RecordTrade(int quantity) =>
        UnitsTradedThisSeason = checked(UnitsTradedThisSeason + quantity);

    internal void ResetTradeQuota() => UnitsTradedThisSeason = 0;

    internal void StartUpgrade(BuildingUpgradeDefinition upgrade)
    {
        if (!IsComplete || IsUpgrading || Definition.Id != upgrade.FromBuildingId)
            throw new InvalidOperationException("This building cannot start the requested upgrade.");
        _pendingUpgrade = upgrade;
        UpgradeSeasonsRemaining = upgrade.ConstructionSeasons;
    }

    internal string? ProgressUpgrade()
    {
        if (_pendingUpgrade is null) return null;
        UpgradeSeasonsRemaining--;
        if (UpgradeSeasonsRemaining > 0) return null;

        var originalName = Definition.Name;
        Definition = BuildingCatalog.Get(_pendingUpgrade.ToBuildingId);
        _pendingUpgrade = null;
        return $"{originalName} → {Definition.Name}";
    }
}

public sealed class Settlement
{
    private readonly List<Building> _buildings = new();
    private int _nextBuildingId;
    public IReadOnlyList<Building> Buildings => _buildings;
    public int Population { get; private set; }
    public int Happiness { get; private set; }
    public EconomyMode EconomyMode { get; }
    public TaxPolicy TaxPolicy { get; private set; } = LittleEmpires.Core.TaxPolicy.Normal;

    // A completed Town Hall, not population alone, grants Town privileges.
    // Progression stays unlocked if population later declines.
    public SettlementTier Tier => _buildings.Any(building =>
        building.IsComplete && building.Definition.Id == BuildingCatalog.TownHallId)
        ? SettlementTier.Town : SettlementTier.Village;

    public bool IsBuildingUnlocked(BuildingDefinition definition) =>
        definition != null &&
        (EconomyMode == EconomyMode.Classic || Tier >= definition.RequiredTier);

    public bool CanStartBuilding(BuildingDefinition definition) =>
        definition != null && definition.CanBuildDirectly &&
        IsBuildingUnlocked(definition) && !WouldDuplicateCivicHall(definition);

    // The founding residents already have homes, but no spare housing.
    public int StartingHousingCapacity { get; }
    public int HousingCapacity => checked(StartingHousingCapacity +
        _buildings.Where(building => building.IsComplete)
            .Sum(building => building.Definition.HousingCapacity));
    public int AvailableHousing => Math.Max(0, HousingCapacity - Population);

    private bool HasCivicHall => _buildings.Any(building =>
        building.Definition.Id == BuildingCatalog.VillageHallId ||
        building.Definition.Id == BuildingCatalog.TownHallId);

    private bool WouldDuplicateCivicHall(BuildingDefinition definition) =>
        (definition.Id == BuildingCatalog.VillageHallId ||
         definition.Id == BuildingCatalog.TownHallId) && HasCivicHall;

    public Settlement(int population = 0, int happiness = 70,
        EconomyMode economyMode = EconomyMode.Classic)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (happiness < 0 || happiness > 100)
            throw new ArgumentOutOfRangeException(nameof(happiness),
                "Happiness must be between 0 and 100.");
        if (!Enum.IsDefined(typeof(EconomyMode), economyMode))
            throw new ArgumentOutOfRangeException(nameof(economyMode));
        StartingHousingCapacity = population;
        Population = population;
        Happiness = happiness;
        EconomyMode = economyMode;
    }

    public void SetTaxPolicy(TaxPolicy policy)
    {
        if (!Enum.IsDefined(typeof(TaxPolicy), policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        TaxPolicy = policy;
    }

    public bool SetWorkPriority(int buildingIndex, int priority)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildings.Count ||
            priority is < -10 or > 10) return false;
        _buildings[buildingIndex].SetWorkPriority(priority);
        return true;
    }

    internal int AdjustHappiness(int amount)
    {
        var before = Happiness;
        Happiness = (int)Math.Max(0L, Math.Min(100L, (long)Happiness + amount));
        return Happiness - before;
    }

    // Primarily for scenario setup; regular population growth uses AcceptImmigrants.
    public void SetPopulation(int population)
    {
        if (population < 0 || population > HousingCapacity)
            throw new ArgumentOutOfRangeException(nameof(population),
                "Population must fit within completed housing capacity.");
        Population = population;
    }

    internal int AcceptImmigrants(int requested)
    {
        if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested));
        var accepted = Math.Min(requested, AvailableHousing);
        Population = checked(Population + accepted);
        return accepted;
    }

    /// <summary>
    /// Place a building in the world, checking terrain, proximity and occupancy
    /// before charging its construction costs. This is the preferred API for
    /// world-based gameplay. The non-spatial overload remains for older tests
    /// and pre-world simulation scenarios.
    /// </summary>
    public bool TryStartConstruction(
        GameState state, IslandMap island, GridPosition position, string buildingId)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (island == null) throw new ArgumentNullException(nameof(island));

        var definition = BuildingCatalog.Get(buildingId);
        definition.Validate();
        if (!CanStartBuilding(definition))
            return false;
        if (!BuildingPlacementRules.Evaluate(island, position, definition).Allowed)
            return false;
        if (!state.TrySpend(definition.Cost))
            return false;

        // Turn commands are single-threaded. The complete footprint was validated above,
        // so these changes cannot fail independently in normal gameplay.
        var building = new Building(_nextBuildingId++, definition, position);
        island.Occupy(position, building);
        _buildings.Add(building);
        return true;
    }

    // Legacy scenario API without a spatial island.
    public bool TryStartConstruction(GameState state, string buildingId) =>
        TryStartConstruction(state, BuildingCatalog.Get(buildingId));

    public bool TryStartConstruction(GameState state, BuildingDefinition definition)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        definition.Validate();
        if (!CanStartBuilding(definition) || !state.TrySpend(definition.Cost))
            return false;
        _buildings.Add(new Building(_nextBuildingId++, definition));
        return true;
    }

    /// <summary>
    /// Upgrade a completed building by its index in Buildings.
    /// World commands resolve persistent identities before calling this legacy API.
    /// </summary>
    public bool TryStartUpgrade(GameState state, int buildingIndex)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (buildingIndex < 0 || buildingIndex >= _buildings.Count) return false;

        var building = _buildings[buildingIndex];
        if (!building.IsComplete || building.IsUpgrading ||
            !BuildingUpgradeCatalog.TryGet(building.Definition.Id, out var upgrade))
            return false;

        upgrade!.Validate();
        // Classic preserves the legacy simulator fixtures; gameplay is Strategic.
        if (EconomyMode == EconomyMode.Strategic &&
            Population < upgrade.MinimumPopulation) return false;
        if (!state.TrySpend(upgrade.Cost)) return false;
        building.StartUpgrade(upgrade);
        return true;
    }

    internal int IndexOfBuilding(int instanceId) =>
        _buildings.FindIndex(building => building.InstanceId == instanceId);

    internal DemolitionResult PreviewDemolition(int instanceId)
    {
        var index = IndexOfBuilding(instanceId);
        if (index < 0) return new DemolitionResult(DemolitionFailure.BuildingNotFound);
        var building = _buildings[index];
        if (building.IsComplete && building.Definition.HousingCapacity > 0)
        {
            var shortfall = Population - (HousingCapacity - building.Definition.HousingCapacity);
            if (shortfall > 0) return new DemolitionResult(DemolitionFailure.InsufficientHousing, shortfall);
        }
        return new DemolitionResult(DemolitionFailure.None);
    }

    internal DemolitionResult Demolish(IslandMap island, int instanceId)
    {
        var preview = PreviewDemolition(instanceId);
        if (!preview.Succeeded) return preview;
        var index = IndexOfBuilding(instanceId);
        island.Release(_buildings[index]);
        _buildings.RemoveAt(index);
        return preview;
    }

    internal IReadOnlyList<string> ProgressUpgrades()
    {
        var completed = new List<string>();
        foreach (var building in _buildings.Where(building => building.IsUpgrading))
        {
            var result = building.ProgressUpgrade();
            if (result is not null) completed.Add(result);
        }
        return completed;
    }

    internal void ResetTradeQuotas()
    {
        foreach (var building in _buildings)
            building.ResetTradeQuota();
    }

    internal void Produce(GameState state)
    {
        foreach (var building in _buildings.Where(b => b.IsComplete))
            foreach (var (resource, amount) in building.Definition.ProductionPerSeason)
                state.Add(resource, amount);
    }

    internal void ProgressConstruction()
    {
        foreach (var building in _buildings.Where(b => !b.IsComplete))
            building.ProgressConstruction();
    }

    // Compatibility helper; use TurnEngine.EndTurn for complete game turns.
    public void ResolveSeason(GameState state)
    {
        Produce(state);
        ProgressConstruction();
        ProgressUpgrades();
        state.AdvanceSeason();
    }
}
}
