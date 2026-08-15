namespace backend.Models.Routing;

public class NearbyJeepneyResponse
{
    public string RouteId { get; set; } = string.Empty;

    public string RouteName { get; set; } = string.Empty;

    public double RouteDistanceMeters { get; set; }

    public double NearestPointLatitude { get; set; }

    public double NearestPointLongitude { get; set; }

    public double WalkingDistanceMeters { get; set; }

    public double WalkingTimeSeconds { get; set; }
}