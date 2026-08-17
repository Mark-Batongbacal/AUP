using backend.Models.Database;

namespace backend.Services.Transportation;

public interface ITransportRouteService
{
    Task<List<TransportRoute>> GetAllActiveRoutesAsync(CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetRouteByIdAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetRouteByCodeAsync(string routeCode, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetLatestRouteWithPolylineAsync(CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetRoutesByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default);

    Task<TransportRouteDetailsDto?> GetRouteDetailsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetRouteStopsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRouteCreationResult> CreateJeepneyRouteAsync(
        CreateJeepneyRouteCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record CreateJeepneyRouteCommand(
    string? RouteCode,
    string? RouteName,
    string? OriginName,
    string? DestinationName,
    List<List<double>>? Points,
    string? Description = null,
    decimal? BaseFare = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public List<List<double>>? Waypoints { get; init; }
}

public enum TransportRouteCreationStatus
{
    Success,
    ValidationFailed,
    DuplicateRouteCode,
    JeepneyModeNotFound,
}

public sealed record TransportRouteCreationResult(
    TransportRouteCreationStatus Status,
    IReadOnlyList<string> Errors,
    TransportRoute? Route)
{
    public static TransportRouteCreationResult Success(TransportRoute route) =>
        new(TransportRouteCreationStatus.Success, [], route);

    public static TransportRouteCreationResult Failure(
        TransportRouteCreationStatus status,
        params string[] errors) => new(status, errors, null);
}

public sealed record TransportRouteDetailsDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    int TransportModeId,
    TransportModeSummaryDto? TransportMode,
    long? StartStopId,
    TransportStopSummaryDto? StartStop,
    long? EndStopId,
    TransportStopSummaryDto? EndStop,
    string? RouteDescription,
    decimal? BaseFare,
    int? EstimatedTotalMinutes,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    int? AverageHeadwayMinutes,
    bool OperatesMonday,
    bool OperatesTuesday,
    bool OperatesWednesday,
    bool OperatesThursday,
    bool OperatesFriday,
    bool OperatesSaturday,
    bool OperatesSunday,
    IReadOnlyList<RouteStopDto> Stops,
    IReadOnlyList<RouteSegmentDto> Segments,
    IReadOnlyList<FareRuleDto> FareRules);

public sealed record TransportModeSummaryDto(
    int TransportModeId,
    string Code,
    string Name,
    bool IsMotorized,
    bool AllowsLiveDriver,
    string? IconName);

public sealed record TransportStopSummaryDto(
    long StopId,
    string? StopCode,
    string Name,
    string? Description,
    string StopType,
    string? Address,
    double Latitude,
    double Longitude);

public sealed record RouteStopDto(
    long RouteStopId,
    long StopId,
    int StopOrder,
    int? EstimatedMinutesFromStart,
    bool CanBoard,
    bool CanAlight,
    TransportStopSummaryDto? Stop);

public sealed record RouteSegmentDto(
    long SegmentId,
    long FromStopId,
    TransportStopSummaryDto? FromStop,
    long ToStopId,
    TransportStopSummaryDto? ToStop,
    int SegmentOrder,
    int? DistanceMeters,
    int? EstimatedDurationSeconds,
    decimal? SegmentFare);

public sealed record FareRuleDto(
    long FareRuleId,
    int TransportModeId,
    long RouteId,
    string RuleName,
    decimal BaseFare,
    int? IncludedDistanceMeters,
    decimal? AdditionalFarePerKm,
    decimal? MinimumFare,
    decimal? MaximumFare,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
