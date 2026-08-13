using backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/test")]
public class RepositoryTestController : ControllerBase
{
    private readonly ITransportRouteRepository _transportRouteRepository;

    public RepositoryTestController(
        ITransportRouteRepository transportRouteRepository)
    {
        _transportRouteRepository = transportRouteRepository;
    }

    /// <summary>
    /// Tests whether TransportRouteRepository can retrieve
    /// route data from Supabase through EF Core.
    /// </summary>
    /// <returns>
    /// A simplified list of active transport routes.
    /// </returns>
    [HttpGet("transport-routes")]
    public async Task<IActionResult> GetTransportRoutes()
    {
        var routes = await _transportRouteRepository.GetAllActiveAsync();

        var result = routes.Select(route => new
        {
            route.RouteId,
            route.RouteCode,
            route.RouteName,
            route.IsActive
        });

        return Ok(result);
    }
}