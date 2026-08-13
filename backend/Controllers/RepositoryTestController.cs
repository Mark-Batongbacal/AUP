using backend.Repositories;
using Microsoft.AspNetCore.Mvc;
using backend.Models.Database;

namespace backend.Controllers;

[ApiController]
[Route("api/test")]
public class RepositoryTestController : ControllerBase
{
    private readonly ITransportRouteRepository _transportRouteRepository;
    private readonly IUserProfileRepository _userProfileRepository;

    public RepositoryTestController(
        ITransportRouteRepository transportRouteRepository,
        IUserProfileRepository userProfileRepository)
    {
        _transportRouteRepository = transportRouteRepository;
        _userProfileRepository = userProfileRepository;
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

    [HttpPost("new-user-profile")]
    public async Task<IActionResult> CreateUserProfile([FromBody] UserProfile userProfile)
    {
        if (userProfile == null)
        {
            return BadRequest("UserProfile cannot be null.");
        }

        await _userProfileRepository.AddOrUpdateAsync(userProfile);
        return CreatedAtAction(nameof(CreateUserProfile), new { id = userProfile.UserId }, userProfile);
    }
}