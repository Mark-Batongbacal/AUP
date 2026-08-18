namespace backend.Services.Navigation;

public sealed record LocationUpdate(
    double Latitude, double Longitude, double AccuracyMeters, DateTime Timestamp,
    double? SpeedMetersPerSecond = null, double? BearingDegrees = null);

public sealed record LocationUpdateResult(
    bool Accepted, string Status, double? DistanceFromLegStartMeters = null,
    double? DistanceFromRouteStartMeters = null, double? DistanceFromGeometryMeters = null,
    IReadOnlyList<backend.Models.Database.NavigationInstruction>? TriggeredInstructions = null);

public sealed record RouteMatch(
    double Latitude, double Longitude, double DistanceFromGeometryMeters,
    double DistanceFromLegStartMeters, double DistanceFromRouteStartMeters,
    int SegmentIndex, double SegmentFraction);
