namespace backend.Services;

public interface IRoutePointService
{
    Task<List<RoutePointDetailsDto>> GetRoutePointsAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<RoutePointReplacementResult> ReplaceRoutePointsAsync(
        long routeId,
        List<List<double>> routePoints,
        CancellationToken cancellationToken = default);
}

public enum RoutePointReplacementStatus
{
    Success,
    ValidationFailed,
    RouteNotFound,
}

public sealed record RoutePointReplacementResult(
    RoutePointReplacementStatus Status,
    IReadOnlyList<string> Errors,
    IReadOnlyList<RoutePointDetailsDto> RoutePoints)
{
    public static RoutePointReplacementResult Success(IReadOnlyList<RoutePointDetailsDto> routePoints) =>
        new(RoutePointReplacementStatus.Success, [], routePoints);

    public static RoutePointReplacementResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(RoutePointReplacementStatus.ValidationFailed, errors, []);

    public static RoutePointReplacementResult RouteNotFound(long routeId) =>
        new(RoutePointReplacementStatus.RouteNotFound, [$"Transport route {routeId} was not found."], []);
}

public sealed record RoutePointDetailsDto(
    long RoutePointId,
    long RouteId,
    int PointOrder,
    double Latitude,
    double Longitude);
