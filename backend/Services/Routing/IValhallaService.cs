using backend.Models.Valhalla;

namespace backend.Services.Routing;

public interface IValhallaService
{
    Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default);
}
