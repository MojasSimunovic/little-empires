namespace LittleEmpires.Core
{

public enum DemolitionFailure
{
    None,
    BuildingNotFound,
    InsufficientHousing
}

public sealed record DemolitionResult(DemolitionFailure Failure, int HousingShortfall = 0)
{
    public bool Succeeded => Failure == DemolitionFailure.None;
}
}
