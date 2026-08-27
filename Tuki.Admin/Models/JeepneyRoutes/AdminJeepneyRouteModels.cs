using System.ComponentModel.DataAnnotations;

namespace Tuki.Admin.Models.JeepneyRoutes;

public sealed record AdminJeepneyRoute(
    long RouteId,
    string RouteCode,
    string RouteName,
    string OriginName,
    string DestinationName,
    string? DirectionName,
    string? OperatorName,
    string? Description,
    decimal? BaseFare,
    bool IsActive,
    int PointCount,
    int WaypointCount,
    bool HasPolyline,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class AdminJeepneyRouteRequest
{
    [Required, StringLength(50)]
    public string? RouteCode { get; set; }

    [Required, StringLength(200)]
    public string? RouteName { get; set; }

    [Required, StringLength(200)]
    public string? OriginName { get; set; }

    [Required, StringLength(200)]
    public string? DestinationName { get; set; }

    [StringLength(100)]
    public string? DirectionName { get; set; }

    [StringLength(200)]
    public string? OperatorName { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? BaseFare { get; set; }
}

public sealed record AdminJeepneyRouteGeometryPoint(
    int PointOrder,
    double Latitude,
    double Longitude);

public sealed record AdminJeepneyRouteGeometry(
    long RouteId,
    string RouteCode,
    string RouteName,
    string OriginName,
    string DestinationName,
    bool IsActive,
    string? EncodedPolyline,
    IReadOnlyList<AdminJeepneyRouteGeometryPoint> Points,
    DateTime? UpdatedAt);

public sealed class AdminJeepneyRouteGeometryRequest
{
    public List<AdminJeepneyRouteGeometryPointRequest> Points { get; set; } = [];
}

public sealed class AdminJeepneyRouteGeometryPointRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class AdminJeepneyValhallaRequest
{
    public List<AdminJeepneyRouteGeometryPointRequest> Waypoints { get; set; } = [];
}

public sealed record AdminJeepneyValhallaPreview(
    long RouteId,
    IReadOnlyList<AdminJeepneyRouteGeometryPoint> Waypoints,
    IReadOnlyList<AdminJeepneyRouteGeometryPoint> GeneratedPoints,
    string EncodedPolyline);

public sealed record AdminJeepneyRouteReadinessCheck(
    string Code,
    string Label,
    bool IsReady,
    string Message);

public sealed record AdminJeepneyRoutePublishReadiness(
    long RouteId,
    bool IsActive,
    bool CanPublish,
    IReadOnlyList<AdminJeepneyRouteReadinessCheck> Checks);

public sealed record AdminJeepneyBackendError(IReadOnlyList<string> Errors);

public sealed record AdminJeepneyRepositoryResult<T>(
    bool Succeeded,
    int StatusCode,
    T? Value,
    string? ErrorMessage)
{
    public static AdminJeepneyRepositoryResult<T> Success(T value, int statusCode = 200) =>
        new(true, statusCode, value, null);

    public static AdminJeepneyRepositoryResult<T> Failure(int statusCode, string message) =>
        new(false, statusCode, default, message);
}
