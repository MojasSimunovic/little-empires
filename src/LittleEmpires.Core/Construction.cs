namespace LittleEmpires.Core;

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
    int UpkeepCoinsPerSeason = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("Building ID is required.");
        if (ConstructionSeasons < 1) throw new ArgumentOutOfRangeException(nameof(ConstructionSeasons));
        if (HousingCapacity < 0) throw new ArgumentOutOfRangeException(nameof(HousingCapacity));
        if (WorkersRequired < 0) throw new ArgumentOutOfRangeException(nameof(WorkersRequired));
        if (UpkeepCoinsPerSeason < 0) throw new ArgumentOutOfRangeException(nameof(UpkeepCoinsPerSeason));
        if (!Enum.IsDefined(SiteRequirement)) throw new ArgumentOutOfRangeException(nameof(SiteRequirement));
        if (Cost.Values.Any(v => v < 0) || ProductionPerSeason.Values.Any(v => v < 0))
            throw new ArgumentException("Building amounts cannot be negative.");
    }
}

public sealed class Building
{
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

    internal Building(BuildingDefinition definition, GridPosition? position = null)
    {
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
    public IReadOnlyList<Building> Buildings => _buildings;
    public int Population { get; private set; }
    public int Happiness { get; private set; }
    public EconomyMode EconomyMode { get; }
    public TaxPolicy TaxPolicy { get; private set; } = TaxPolicy.Normal;

    // The founding residents already have homes, but no spare housing.
    public int StartingHousingCapacity { get; }
    public int HousingCapacity => checked(StartingHousingCapacity +
        _buildings.Where(building => building.IsComplete)
            .Sum(building => building.Definition.HousingCapacity));
    public int AvailableHousing => Math.Max(0, HousingCapacity - Population);

    public Settlement(int population = 0, int happiness = 70,
        EconomyMode economyMode = EconomyMode.Classic)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (happiness < 0 || happiness > 100)
            throw new ArgumentOutOfRangeException(nameof(happiness),
                "Happiness must be between 0 and 100.");
        if (!Enum.IsDefined(economyMode))
            throw new ArgumentOutOfRangeException(nameof(economyMode));
        StartingHousingCapacity = population;
        Population = population;
        Happiness = happiness;
        EconomyMode = economyMode;
    }

    public void SetTaxPolicy(TaxPolicy policy)
    {
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
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
        Happiness = (int)Math.Clamp((long)Happiness + amount, 0L, 100L);
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
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(island);

        var definition = BuildingCatalog.Get(buildingId);
        definition.Validate();
        if (!BuildingPlacementRules.Evaluate(island, position, definition).Allowed)
            return false;
        if (!state.TrySpend(definition.Cost))
            return false;

        // Turn commands are single-threaded. The tile was validated above,
        // so these changes cannot fail independently in normal gameplay.
        var building = new Building(definition, position);
        island.Occupy(position, building);
        _buildings.Add(building);
        return true;
    }

    // Legacy scenario API without a spatial island.
    public bool TryStartConstruction(GameState state, string buildingId) =>
        TryStartConstruction(state, BuildingCatalog.Get(buildingId));

    public bool TryStartConstruction(GameState state, BuildingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!definition.CanBuildDirectly || !state.TrySpend(definition.Cost)) return false;
        _buildings.Add(new Building(definition));
        return true;
    }

    /// <summary>
    /// Upgrade a completed housing building by its index in Buildings.
    /// Indices remain stable until building removal is introduced.
    /// </summary>
    public bool TryStartUpgrade(GameState state, int buildingIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (buildingIndex < 0 || buildingIndex >= _buildings.Count) return false;

        var building = _buildings[buildingIndex];
        if (!building.IsComplete || building.IsUpgrading ||
            !HousingUpgradeCatalog.TryGet(building.Definition.Id, out var upgrade))
            return false;

        upgrade!.Validate();
        if (!state.TrySpend(upgrade.Cost)) return false;
        building.StartUpgrade(upgrade);
        return true;
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
