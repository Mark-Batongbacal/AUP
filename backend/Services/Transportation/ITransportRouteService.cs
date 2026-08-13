using backend.Models.Database;

namespace backend.Services;

public interface ITransportRouteService
{
    Task<List<TransportRoute>> GetAllActiveRoutesAsync(CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetRouteByIdAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetRouteByCodeAsync(string routeCode, CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetRoutesByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<TransportRouteDetailsDto?> GetRouteDetailsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default);
}

public sealed record TransportRouteDetailsDto(
    Guid RouteId,
    string RouteCode,
    string RouteName,
    short TransportModeId,
    TransportModeSummaryDto? TransportMode,
    Guid? StartStopId,
    TransportStopSummaryDto? StartStop,
    Guid? EndStopId,
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
    short TransportModeId,
    string Code,
    string Name,
    bool IsMotorized,
    bool AllowsLiveDriver,
    string? IconName);

public sealed record TransportStopSummaryDto(
    Guid StopId,
    string? StopCode,
    string Name,
    string? Description,
    string StopType,
    string? Address,
    double Latitude,
    double Longitude);

public sealed record RouteStopDto(
    Guid RouteStopId,
    Guid StopId,
    int StopOrder,
    int? EstimatedMinutesFromStart,
    bool CanBoard,
    bool CanAlight,
    TransportStopSummaryDto? Stop);

public sealed record RouteSegmentDto(
    long SegmentId,
    Guid FromStopId,
    TransportStopSummaryDto? FromStop,
    Guid ToStopId,
    TransportStopSummaryDto? ToStop,
    int SegmentOrder,
    decimal DistanceMeters,
    decimal EstimatedMinutes,
    decimal EstimatedFare,
    bool IsBidirectional);

public sealed record FareRuleDto(
    Guid FareRuleId,
    short TransportModeId,
    Guid? RouteId,
    string RuleName,
    decimal BaseFare,
    decimal? BaseDistanceKm,
    decimal? AdditionalFarePerKm,
    decimal? MinimumFare,
    decimal? MaximumFare,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
