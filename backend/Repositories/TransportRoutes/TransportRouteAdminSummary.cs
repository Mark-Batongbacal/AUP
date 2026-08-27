namespace backend.Repositories;

public sealed record TransportRouteAdminSummary(
    long RouteId,
    string RouteCode,
    string RouteName,
    string OriginName,
    string DestinationName,
    string? DirectionName,
    string? OperatorName,
    string? RouteDescription,
    decimal? BaseFare,
    bool IsActive,
    int PointCount,
    int WaypointCount,
    bool HasPolyline,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public DateTime? ArchivedAt { get; init; }
    public bool IsArchived => ArchivedAt.HasValue;
}
