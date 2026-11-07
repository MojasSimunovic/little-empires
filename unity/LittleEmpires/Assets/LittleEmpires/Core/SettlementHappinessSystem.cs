using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

/// <summary>
/// Provisional seasonal happiness model. A settlement needs both reliable food
/// and room to grow; food shortages hurt more than good seasons help.
/// </summary>
public sealed class SettlementHappinessSystem
{
    public const int FedSeasonBonus = 2;
    public const int FoodShortagePenalty = -12;
    public const int SpareHousingBonus = 1;
    public const int FullHousingPenalty = -1;

    /// <returns>The actual happiness change after clamping to 0..100.</returns>
    public int ResolveSeason(Settlement settlement, int foodShortage,
        int additionalHappinessChange = 0)
    {
        if (settlement == null) throw new ArgumentNullException(nameof(settlement));
        if (foodShortage < 0) throw new ArgumentOutOfRangeException(nameof(foodShortage));
        if (settlement.Population == 0) return 0;

        var foodEffect = foodShortage > 0 ? FoodShortagePenalty : FedSeasonBonus;
        var housingEffect = settlement.AvailableHousing > 0
            ? SpareHousingBonus
            : FullHousingPenalty;
        return settlement.AdjustHappiness(checked(foodEffect + housingEffect +
            additionalHappinessChange));
    }
}
}
