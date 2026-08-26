using System.ComponentModel.DataAnnotations;

namespace backend.Models.JeepneyRouteManagement;

public sealed class AdminJeepneyRouteMutationRequest
{
    [Required, StringLength(50)]
    public string? RouteCode { get; init; }

    [Required, StringLength(200)]
    public string? RouteName { get; init; }

    [Required, StringLength(200)]
    public string? OriginName { get; init; }

    [Required, StringLength(200)]
    public string? DestinationName { get; init; }

    [StringLength(100)]
    public string? DirectionName { get; init; }

    [StringLength(200)]
    public string? OperatorName { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? BaseFare { get; init; }
}

public sealed record AdminJeepneyRouteResponse(
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

public enum AdminJeepneyRouteMutationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Conflict,
    JeepneyModeNotFound,
    ActiveRouteLocked
}

public sealed record AdminJeepneyRouteMutationResult(
    AdminJeepneyRouteMutationStatus Status,
    IReadOnlyList<string> Errors,
    AdminJeepneyRouteResponse? Route)
{
    public bool Succeeded => Status == AdminJeepneyRouteMutationStatus.Success;

    public static AdminJeepneyRouteMutationResult Success(AdminJeepneyRouteResponse route) =>
        new(AdminJeepneyRouteMutationStatus.Success, [], route);

    public static AdminJeepneyRouteMutationResult Failure(
        AdminJeepneyRouteMutationStatus status,
        params string[] errors) => new(status, errors, null);
}
