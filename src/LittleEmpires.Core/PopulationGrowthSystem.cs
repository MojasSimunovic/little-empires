namespace LittleEmpires.Core;

/// <summary>
/// First-pass immigration policy: housing plus a food surplus attracts new residents.
/// Surplus food is not consumed by immigration; arrivals begin eating next season.
/// </summary>
public sealed class PopulationGrowthSystem
{
    public const int MaxImmigrantsPerSeason = 2;

    public int ResolveImmigration(GameState state, Settlement settlement, int foodShortage)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);
        if (foodShortage < 0) throw new ArgumentOutOfRangeException(nameof(foodShortage));

        if (foodShortage > 0)
            return 0;

        // Require one unit of remaining food per newcomer as a simple
        // food-security proxy. The food itself stays in the stockpile.
        var eligibleResidents = Math.Min(MaxImmigrantsPerSeason, state.Get(Resource.Food));
        return settlement.AcceptImmigrants(eligibleResidents);
    }
}
