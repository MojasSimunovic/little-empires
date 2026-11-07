using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

/// <summary>
/// Classic preserves the early prototype's economy and existing deterministic
/// benchmarks. Strategic enables taxes, workforce and upkeep for the real game.
/// Unity should create Strategic settlements; Classic is for legacy demos.
/// </summary>
public enum EconomyMode { Classic, Strategic }

public enum TaxPolicy { Low, Normal, High, VeryHigh }

public readonly struct TaxPolicyEffects
{
    public int IncomeNumerator { get; }
    public int IncomeDenominator { get; }
    public int HappinessPerSeason { get; }
    public TaxPolicyEffects(int incomeNumerator, int incomeDenominator, int happinessPerSeason)
    {
        IncomeNumerator = incomeNumerator;
        IncomeDenominator = incomeDenominator;
        HappinessPerSeason = happinessPerSeason;
    }
}

/// <summary>
/// Fixed-rate prototype taxes. Tax revenue uses existing residents only, so
/// newcomers begin contributing on the following season.
/// </summary>
public static class TaxationSystem
{
    public static TaxPolicyEffects Effects(TaxPolicy policy) => policy switch
    {
        TaxPolicy.Low => new TaxPolicyEffects(1, 2, +2),
        TaxPolicy.Normal => new TaxPolicyEffects(2, 2, 0),
        TaxPolicy.High => new TaxPolicyEffects(3, 2, -4),
        TaxPolicy.VeryHigh => new TaxPolicyEffects(4, 2, -8),
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };

    public static int CalculateIncome(int population, TaxPolicy policy)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        var rate = Effects(policy);
        return checked((int)((long)population * rate.IncomeNumerator / rate.IncomeDenominator));
    }

    public static int Collect(GameState state, int population, TaxPolicy policy)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var income = CalculateIncome(population, policy);
        state.Add(Resource.Coins, income);
        return income;
    }
}
}
