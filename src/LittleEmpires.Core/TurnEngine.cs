namespace LittleEmpires.Core;

/// <summary>A report for one completed seasonal turn.</summary>
public sealed record TurnSummary(
    int StartingYear,
    Season StartingSeason,
    int EndingYear,
    Season EndingSeason,
    IReadOnlyDictionary<Resource, int> ResourceChanges,
    IReadOnlyList<string> CompletedBuildings,
    int Population,
    int FoodRequired,
    int FoodConsumed,
    int FoodShortage,
    int StartingPopulation,
    int NewResidents,
    int HousingCapacity,
    int StartingHappiness,
    int Happiness,
    int HappinessChange,
    IReadOnlyList<string> CompletedUpgrades,
    int TaxIncome,
    int OperatingCosts,
    int WorkingResidents,
    int UnemployedResidents,
    IReadOnlyDictionary<Resource, int> GrossProduction);

/// <summary>
/// Resolves a strategic season: taxes, workforce/upkeep, production, food,
/// construction/upgrades, happiness, immigration, then calendar advancement. New buildings produce from the next turn.
/// </summary>
public sealed class TurnEngine
{
    private readonly PopulationGrowthSystem _growth = new();
    private readonly SettlementHappinessSystem _happiness = new();

    public TurnSummary EndTurn(GameState state, Settlement settlement,
        IslandMap? island = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);

        var startingYear = state.Year;
        var startingSeason = state.Season;
        var startingPopulation = settlement.Population;
        var startingHappiness = settlement.Happiness;
        var before = Enum.GetValues<Resource>()
            .ToDictionary(resource => resource, state.Get);
        var incomplete = settlement.Buildings
            .Where(building => !building.IsComplete)
            .ToHashSet();

        // Taxes use the starting population, before new immigrants arrive.
        var taxIncome = settlement.EconomyMode == EconomyMode.Strategic
            ? TaxationSystem.Collect(state, startingPopulation, settlement.TaxPolicy)
            : 0;

        // With Strategic rules, buildings must have available workers and be
        // able to pay upkeep. Classic mode retains old prototype benchmarks.
        var staffing = settlement.EconomyMode == EconomyMode.Strategic
            ? WorkforceSystem.Resolve(state, settlement)
            : WorkforceSystem.Classic(settlement);

        // Existing production feeds current residents this season. Newly
        // completed structures begin generating output next season.
        var production = ProductionSystem.Resolve(state, settlement, island, staffing);
        var foodRequired = startingPopulation;
        var foodConsumed = Math.Min(foodRequired, state.Get(Resource.Food));
        if (foodConsumed > 0)
            state.TrySpend(new Dictionary<Resource, int> { [Resource.Food] = foodConsumed });
        var foodShortage = foodRequired - foodConsumed;
        settlement.ProgressConstruction();
        var completedUpgrades = settlement.ProgressUpgrades();
        var taxHappiness = settlement.EconomyMode == EconomyMode.Strategic
            ? TaxationSystem.Effects(settlement.TaxPolicy).HappinessPerSeason
            : 0;
        var happinessChange = _happiness.ResolveSeason(
            settlement, foodShortage, taxHappiness);
        // New residents arrive after current residents have eaten. They start
        // consuming food on the following seasonal turn.
        var newResidents = _growth.ResolveImmigration(state, settlement, foodShortage);
        state.AdvanceSeason();
        // New season means fresh per-Marketplace trade limits.
        settlement.ResetTradeQuotas();

        var changes = Enum.GetValues<Resource>()
            .ToDictionary(resource => resource, resource => state.Get(resource) - before[resource]);
        var completed = settlement.Buildings
            .Where(building => incomplete.Contains(building) && building.IsComplete)
            .Select(building => building.Definition.Name)
            .ToArray();

        return new TurnSummary(
            startingYear, startingSeason, state.Year, state.Season,
            changes, completed, settlement.Population,
            foodRequired, foodConsumed, foodShortage,
            startingPopulation, newResidents, settlement.HousingCapacity,
            startingHappiness, settlement.Happiness, happinessChange,
            completedUpgrades, taxIncome, staffing.OperatingCosts,
            staffing.WorkingResidents, staffing.UnemployedResidents,
            production.Produced);
    }
}
