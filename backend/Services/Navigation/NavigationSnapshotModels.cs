namespace backend.Services.Navigation;

public sealed record StartNavigationRequest(Guid RecommendationId);

public sealed record NavigationRerouteRequest(
    string Reason = "MANUAL",
    string? Preference = null,
    decimal? Budget = null,
    bool ClearBudget = false,
    string? DestinationName = null,
    double? DestinationLatitude = null,
    double? DestinationLongitude = null,
    string? AvoidTransportMode = null);

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
    bool RequiresConfirmation,
    string? Text = null);

public sealed record NavigationInstructionDetailSnapshot(
    int Sequence,
    string Type,
    int LegIndex,
    string Text,
    string? StreetName,
    double? Latitude,
    double? Longitude,
    double? DistanceFromLegStartMeters,
    double TriggerDistanceMeters,
    bool RequiresConfirmation);

public sealed record NavigationLandmarkSnapshot(
    string Name,
    string Category,
    string Role,
    string Relation,
    double Latitude,
    double Longitude,
    double DistanceFromTargetMeters,
    double? DistanceFromRouteStartMeters = null,
    double TriggerBeforeMeters = 0,
    double TriggerAfterMeters = 0);

public sealed record NavigationStopInfo(
    string? RouteName,
    double? Latitude,
    double? Longitude,
    NavigationLandmarkSnapshot? Landmark);

public sealed record NavigationTriggeredEvent(
    string Type,
    string? LandmarkName = null);

public sealed record NavigationTripSummarySnapshot(
    string DestinationName,
    int? DurationMinutes,
    decimal ApproxFareSpent,
    int TransitLegs,
    int Transfers);

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
    decimal EstimatedRemainingFare,
    NavigationInstructionSnapshot? FollowingInstruction = null,
    NavigationTripSummarySnapshot? TripSummary = null,
    string? SpokenInstructionTemplate = null,
    IReadOnlyList<NavigationInstructionDetailSnapshot>? CurrentLegInstructions = null,
    IReadOnlyList<NavigationLandmarkSnapshot>? CurrentLegLandmarks = null);

public sealed record NavigationOperation(NavigationSnapshot? Snapshot, string? Error = null)
{
    public bool Succeeded => Snapshot is not null;
}
