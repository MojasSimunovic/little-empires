using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

/// <summary>
/// First-pass immigration policy: housing, food surplus, and happiness attract new residents.
/// Surplus food is not consumed by immigration; arrivals begin eating next season.
/// </summary>
public sealed class PopulationGrowthSystem
{
    public const int MaxImmigrantsPerSeason = 2;

    public int ResolveImmigration(GameState state, Settlement settlement, int foodShortage)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (settlement == null) throw new ArgumentNullException(nameof(settlement));
        if (foodShortage < 0) throw new ArgumentOutOfRangeException(nameof(foodShortage));

        if (foodShortage > 0)
            return 0;

        // Happiness is resolved before immigration each season.
        // Unhappy settlements cannot attract residents; moderately happy
        // settlements attract at most one, and thriving ones up to two.
        var happinessLimit = settlement.Happiness switch
        {
            < 50 => 0,
            < 70 => 1,
            _ => MaxImmigrantsPerSeason
        };

        // Require one unit of remaining food per newcomer as a simple
        // food-security proxy. The food itself stays in the stockpile.
        var eligibleResidents = Math.Min(happinessLimit, state.Get(Resource.Food));
        return settlement.AcceptImmigrants(eligibleResidents);
    }
}
}
