using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleEmpires.Core
{

public enum TradeDirection { Buy, Sell }

public enum TradeFailure
{
    None,
    InvalidQuantity,
    InvalidResource,
    InvalidMarket,
    NotConnected,
    SeasonalLimit,
    InsufficientCoins,
    InsufficientStock,
    InventoryOverflow
}

public readonly struct TradeResult
{
    public TradeFailure Failure { get; }
    public int Units { get; }
    public int UnitPrice { get; }
    public int TotalCoins { get; }
    public TradeResult(TradeFailure failure, int units, int unitPrice, int totalCoins)
    {
        Failure = failure;
        Units = units;
        UnitPrice = unitPrice;
        TotalCoins = totalCoins;
    }
    public bool Succeeded => Failure == TradeFailure.None;
}

/// <summary>
/// Player-operated Marketplace trade with fixed initial buy/sell spreads.
/// A completed market must connect by roads to a completed residential
/// building. Each market has a 30-unit trading capacity per season.
/// </summary>
public static class MarketplaceTrading
{
    public const int MaxUnitsPerMarketPerSeason = 30;
    public const int MaxUnitsPerHarborPerSeason = 120;

    public static (int Buy, int Sell) Prices(Resource resource) => resource switch
    {
        Resource.Wood => (5, 3),
        Resource.Stone => (8, 5),
        Resource.Food => (4, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(resource),
            "Coins and unknown goods cannot be traded.")
    };

    public static TradeResult Execute(
        GameState state, Settlement settlement, IslandMap island,
        int marketplaceIndex, TradeDirection direction, Resource resource, int units)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (settlement == null) throw new ArgumentNullException(nameof(settlement));
        if (island == null) throw new ArgumentNullException(nameof(island));

        if (units <= 0) return Fail(TradeFailure.InvalidQuantity);
        if (!Enum.IsDefined(typeof(TradeDirection), direction) || resource is not
            (Resource.Wood or Resource.Stone or Resource.Food))
            return Fail(TradeFailure.InvalidResource);

        if (marketplaceIndex < 0 || marketplaceIndex >= settlement.Buildings.Count)
            return Fail(TradeFailure.InvalidMarket);
        var market = settlement.Buildings[marketplaceIndex];
        var isMarketplace = market.Definition.Id == BuildingCatalog.MarketplaceId;
        var isHarbor = market.Definition.Id == BuildingCatalog.TradingHarborId;
        if (!market.IsComplete || (!isMarketplace && !isHarbor) ||
            market.Position is null ||
            !island.TryGetBuilding(market.Position.Value, out var occupant) ||
            !ReferenceEquals(occupant, market))
            return Fail(TradeFailure.InvalidMarket);

        var connectedHousing = settlement.Buildings.Any(house =>
            house.IsComplete && house.Definition.HousingCapacity > 0 &&
            house.Position is not null &&
            island.TryGetBuilding(house.Position.Value, out var residentBuilding) &&
            ReferenceEquals(residentBuilding, house) &&
            island.Roads.AreBuildingsConnected(
                market.Position.Value, house.Position.Value));
        if (!connectedHousing) return Fail(TradeFailure.NotConnected);

        var seasonalLimit = isHarbor ? MaxUnitsPerHarborPerSeason : MaxUnitsPerMarketPerSeason;
        if ((long)market.UnitsTradedThisSeason + units > seasonalLimit)
            return Fail(TradeFailure.SeasonalLimit);

        var price = Prices(resource);
        var unitPrice = direction == TradeDirection.Buy ? price.Buy : price.Sell;
        var total = (long)units * unitPrice;
        if (total > int.MaxValue) return Fail(TradeFailure.InventoryOverflow);

        if (direction == TradeDirection.Buy)
        {
            if (state.Get(Resource.Coins) < total)
                return Fail(TradeFailure.InsufficientCoins);
            if ((long)state.Get(resource) + units > int.MaxValue)
                return Fail(TradeFailure.InventoryOverflow);

            if (!state.TrySpend(new Dictionary<Resource, int>
                { [Resource.Coins] = (int)total }))
                return Fail(TradeFailure.InsufficientCoins);
            state.Add(resource, units);
        }
        else
        {
            if (state.Get(resource) < units)
                return Fail(TradeFailure.InsufficientStock);
            if ((long)state.Get(Resource.Coins) + total > int.MaxValue)
                return Fail(TradeFailure.InventoryOverflow);

            if (!state.TrySpend(new Dictionary<Resource, int>
                { [resource] = units }))
                return Fail(TradeFailure.InsufficientStock);
            state.Add(Resource.Coins, (int)total);
        }

        market.RecordTrade(units);
        return new TradeResult(TradeFailure.None, units, unitPrice, (int)total);
    }

    private static TradeResult Fail(TradeFailure failure) => new(failure, 0, 0, 0);
}
}
