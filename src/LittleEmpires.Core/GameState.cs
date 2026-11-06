namespace LittleEmpires.Core;

public enum Season { Spring, Summer, Autumn, Winter }
public enum Resource { Wood, Stone, Food, Coins }

public sealed class GameState
{
    private readonly Dictionary<Resource, int> _resources = new();
    public int Year { get; private set; } = 1;
    public Season Season { get; private set; } = Season.Spring;
    public IReadOnlyDictionary<Resource, int> Resources => _resources;

    public GameState(int wood = 50, int stone = 25, int food = 30, int coins = 100)
    {
        _resources[Resource.Wood] = RequireNonNegative(wood);
        _resources[Resource.Stone] = RequireNonNegative(stone);
        _resources[Resource.Food] = RequireNonNegative(food);
        _resources[Resource.Coins] = RequireNonNegative(coins);
    }

    public int Get(Resource resource) => _resources.GetValueOrDefault(resource);

    public void Add(Resource resource, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _resources[resource] = checked(Get(resource) + amount);
    }

    public bool CanAfford(IReadOnlyDictionary<Resource, int> cost) =>
        cost.All(entry => entry.Value >= 0 && Get(entry.Key) >= entry.Value);

    public bool TrySpend(IReadOnlyDictionary<Resource, int> cost)
    {
        if (!CanAfford(cost)) return false;
        foreach (var (resource, amount) in cost)
            _resources[resource] -= amount;
        return true;
    }

    public void AdvanceSeason()
    {
        if (Season == Season.Winter)
        {
            Season = Season.Spring;
            Year++;
        }
        else
        {
            Season++;
        }
    }

    private static int RequireNonNegative(int value) =>
        value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
