namespace backend.Services.Navigation;

public sealed record StartNavigationRequest(Guid RecommendationId);

public sealed record NavigationRerouteRequest(
    string Reason = "MANUAL",
    string? Preference = null,
    decimal? Budget = null,
    bool ClearBudget = false,
    string? DestinationName = null,
    double? DestinationLatitude = null,
    double? DestinationLongitude = null);

public sealed record NavigationLegSnapshot(
    int LegIndex,
    string TransportMode,
    long? RouteId,
    string? RouteName,
    string? FromName,
    string? ToName,
    double? StartLatitude,
    double? StartLongitude,
    double? EndLatitude,
    double? EndLongitude,
    double? DistanceMeters,
    decimal Fare);

public sealed record NavigationInstructionSnapshot(
    string Type,
    string? RouteName,
    string? TransportMode,
    double? DistanceMeters,
    bool RequiresConfirmation);

public sealed record NavigationLandmarkSnapshot(
    string Name,
    string Category,
    string Role,
    string Relation,
    double Latitude,
    double Longitude,
    double DistanceFromTargetMeters);

public sealed record NavigationStopInfo(
    string? RouteName,
    double? Latitude,
    double? Longitude,
    NavigationLandmarkSnapshot? Landmark);

public sealed record NavigationTriggeredEvent(
    string Type,
    string? LandmarkName = null);

public sealed record NavigationSnapshot(
    Guid SessionId,
    string State,
    int CurrentLegIndex,
    NavigationLegSnapshot? CurrentLeg,
    NavigationInstructionSnapshot? NextInstruction,
    string? SpokenInstruction,
    double? RemainingDistanceMeters,
    double ProgressMeters,
    NavigationStopInfo? BoardInfo,
    NavigationStopInfo? AlightInfo,
    NavigationLandmarkSnapshot? Landmark,
    bool RequiresBoardingConfirmation,
    bool RequiresAlightingConfirmation,
    bool RerouteRequired,
    string Status,
    IReadOnlyList<NavigationTriggeredEvent> TriggeredEvents,
    double? CurrentLatitude,
    double? CurrentLongitude,
    decimal ApproxFareSpent,
    decimal EstimatedRemainingFare);

public sealed record NavigationOperation(NavigationSnapshot? Snapshot, string? Error = null)
{
    public bool Succeeded => Snapshot is not null;
}
