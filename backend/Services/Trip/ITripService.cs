using backend.Models.Database;

namespace backend.Services;

public interface ITripService
{
    Task<TripSearch?> GetTripSearchByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<List<TripSearch>> GetTripSearchesByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TripSearch?> CreateTripSearchAsync(
        Guid? userId,
        string originName,
        double originLatitude,
        double originLongitude,
        string destinationName,
        double destinationLatitude,
        double destinationLongitude,
        int passengerCount = 1,
        decimal? budget = null,
        string? preference = null,
        DateTime? requestedAt = null,
        CancellationToken cancellationToken = default);

    Task<List<RouteRecommendation>> GetRecommendationsForSearchAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<RouteRecommendation?> GetRecommendationByIdAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<RecommendationDetailsDto?> GetRecommendationDetailsAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<PassengerTrip?> GetPassengerTripByIdAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<PassengerTrip>> GetPassengerTripsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PassengerTrip?> StartPassengerTripAsync(
        Guid userId,
        Guid recommendationId,
        DateTime? startedAt = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdatePassengerTripStatusAsync(
        Guid passengerTripId,
        string status,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateCurrentLegAsync(
        Guid passengerTripId,
        int currentLegOrder,
        CancellationToken cancellationToken = default);

    Task<PassengerTripDetailsDto?> GetPassengerTripDetailsAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<TripAlert>> GetTripAlertsAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<TripAlert>> GetPendingTripAlertsAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<TripAlert?> CreateTripAlertAsync(
        Guid passengerTripId,
        string alertType,
        string message,
        Guid? legId = null,
        long? targetStopId = null,
        string? title = null,
        decimal? triggerDistanceMeters = null,
        CancellationToken cancellationToken = default);

    Task<bool> MarkTripAlertTriggeredAsync(
        Guid alertId,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default);
}

public sealed record RecommendationDetailsDto(
    Guid RecommendationId,
    Guid TripSearchId,
    string RecommendationType,
    int RankNumber,
    decimal TotalFare,
    decimal TotalMinutes,
    decimal? TotalDistanceMeters,
    decimal WalkingDistanceMeters,
    int TransferCount,
    decimal? RecommendationScore,
    string? Explanation,
    DateTime GeneratedAt,
    IReadOnlyList<RecommendationLegDto> Legs);

public sealed record RecommendationLegDto(
    Guid LegId,
    Guid RecommendationId,
    int LegOrder,
    int TransportModeId,
    TransportModeSummaryDto? TransportMode,
    long? RouteId,
    TransportRouteSummaryDto? Route,
    long? FromStopId,
    TransportStopSummaryDto? FromStop,
    long? ToStopId,
    TransportStopSummaryDto? ToStop,
    string? FromName,
    string? ToName,
    double? StartLatitude,
    double? StartLongitude,
    double? EndLatitude,
    double? EndLongitude,
    decimal? DistanceMeters,
    decimal EstimatedMinutes,
    decimal EstimatedFare,
    string? Instructions,
    DateTime CreatedAt);

public sealed record TransportRouteSummaryDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    int TransportModeId,
    decimal? BaseFare,
    int? EstimatedTotalMinutes,
    bool IsActive);

public sealed record PassengerTripDetailsDto(
    Guid PassengerTripId,
    Guid UserId,
    Guid RecommendationId,
    int CurrentLegOrder,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    RecommendationDetailsDto? Recommendation,
    IReadOnlyList<TripAlertDto> Alerts);

public sealed record TripAlertDto(
    Guid AlertId,
    Guid PassengerTripId,
    Guid? LegId,
    long? TargetStopId,
    TransportStopSummaryDto? TargetStop,
    string AlertType,
    string? Title,
    string Message,
    decimal? TriggerDistanceMeters,
    bool IsTriggered,
    DateTime? TriggeredAt,
    DateTime CreatedAt);
