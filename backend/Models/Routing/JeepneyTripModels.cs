namespace backend.Models.Routing;

/// <summary>
/// A fixed tricycle terminal loaded from TestData/trike-points.json.
/// </summary>
public sealed record TrikePoint(
    string Id,
    string Name,
    double Latitude,
    double Longitude);

public enum AccessMode
{
    Walk,
    Trike,
    Jeepney
}

public sealed class JeepneyAccessSegment
{
    public required AccessMode Mode { get; init; }
    public double WalkDistanceMeters { get; init; }
    public double WalkTimeSeconds { get; init; }
    public string? TrikePointId { get; init; }
    public string? TrikePointName { get; init; }
    public double? TrikePointLatitude { get; init; }
    public double? TrikePointLongitude { get; init; }
    public double? TrikeRideDistanceMeters { get; init; }
    public double? TrikeRideTimeSeconds { get; init; }
    public double TotalTimeSeconds { get; init; }
    public double TotalFarePesos { get; init; }
    public double GeneralizedCostPesos { get; init; }
}

public sealed class JeepneyTripOption
{
    public required string RouteId { get; init; }
    public required string RouteName { get; init; }

    public double BoardLatitude { get; init; }
    public double BoardLongitude { get; init; }
    public required JeepneyAccessSegment BoardAccess { get; init; }

    public double AlightLatitude { get; init; }
    public double AlightLongitude { get; init; }
    public required JeepneyAccessSegment AlightAccess { get; init; }

    public double TotalTimeSeconds { get; init; }
    public double TotalFarePesos { get; init; }
    public double GeneralizedCostPesos { get; init; }
}

public sealed class JeepneyTripLeg
{
    public required AccessMode Mode { get; init; }
    public string? RouteId { get; init; }
    public string? RouteName { get; init; }
    public double BoardLatitude { get; init; }
    public double BoardLongitude { get; init; }
    public double AlightLatitude { get; init; }
    public double AlightLongitude { get; init; }
    public double OriginLatitude { get; init; }
    public double OriginLongitude { get; init; }
    public double DestinationLatitude { get; init; }
    public double DestinationLongitude { get; init; }
    public double DistanceMeters { get; init; }
    public double DurationSeconds { get; init; }
    public double FarePesos { get; init; }
    public double GeneralizedCostPesos { get; init; }
    public double? WalkDistanceMeters { get; init; }
    public double? WalkTimeSeconds { get; init; }
    public double? TrikeDistanceMeters { get; init; }
    public double? TrikeTimeSeconds { get; init; }
    public double? JeepneyDistanceMeters { get; init; }
    public double? JeepneyTimeSeconds { get; init; }
    public string? TrikePointId { get; init; }
    public string? TrikePointName { get; init; }
}

public sealed class JeepneyTripPlan
{
    public List<JeepneyTripLeg> Legs { get; init; } = [];
    public required JeepneyAccessSegment OriginAccess { get; init; }
    public required JeepneyAccessSegment DestinationAccess { get; init; }
    public List<double> TransferWalkDistancesMeters { get; init; } = [];
    public List<double> TransferWalkTimesSeconds { get; init; } = [];
    public double TotalTimeSeconds { get; set; }
    public double TotalFarePesos { get; set; }
    public double GeneralizedCostPesos { get; set; }

    public int TransferCount =>
        Math.Max(0, Legs.Count(leg => leg.Mode == AccessMode.Jeepney) - 1);
}
