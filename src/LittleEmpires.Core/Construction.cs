namespace LittleEmpires.Core;

public sealed record BuildingDefinition(
    string Id,
    string Name,
    IReadOnlyDictionary<Resource, int> Cost,
    int ConstructionSeasons,
    IReadOnlyDictionary<Resource, int> ProductionPerSeason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("Building ID is required.");
        if (ConstructionSeasons < 1) throw new ArgumentOutOfRangeException(nameof(ConstructionSeasons));
        if (Cost.Values.Any(v => v < 0) || ProductionPerSeason.Values.Any(v => v < 0))
            throw new ArgumentException("Building amounts cannot be negative.");
    }
}

public sealed class Building
{
    public BuildingDefinition Definition { get; }
    public int SeasonsRemaining { get; private set; }
    public bool IsComplete => SeasonsRemaining == 0;

    internal Building(BuildingDefinition definition)
    {
        Definition = definition;
        SeasonsRemaining = definition.ConstructionSeasons;
    }

    internal void ProgressConstruction()
    {
        if (SeasonsRemaining > 0) SeasonsRemaining--;
    }
}

public sealed class Settlement
{
    private readonly List<Building> _buildings = new();
    public IReadOnlyList<Building> Buildings => _buildings;
    public int Population { get; private set; }

    public Settlement(int population = 0)
    {
        SetPopulation(population);
    }

    public void SetPopulation(int population)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        Population = population;
    }

    // The gameplay/UI layer can select one of the catalogue buildings by ID.
    public bool TryStartConstruction(GameState state, string buildingId) =>
        TryStartConstruction(state, BuildingCatalog.Get(buildingId));

    public bool TryStartConstruction(GameState state, BuildingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!state.TrySpend(definition.Cost)) return false;
        _buildings.Add(new Building(definition));
        return true;
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
        state.AdvanceSeason();
    }
}
