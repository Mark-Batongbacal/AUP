using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/routes")]
public sealed class RoutesController : ControllerBase
{
    /// <summary>
    /// GET /api/routes/sample
    ///
    /// Parameters: none.
    ///
    /// Returns a routeName label and an ordered points list. The Kotlin
    /// frontend should convert each point's latitude/longitude into LatLng,
    /// then pass that ordered list to drawRoute(routePoints).
    ///
    /// These coordinates are temporary mock/test data only. Replace this later
    /// with real route data from the database/repository layer; do not add
    /// database queries or route recommendation logic here yet.
    /// </summary>
    [HttpGet("sample")]
    public ActionResult<RouteDto> GetSampleRoute()
    {
        var sampleRoute = new RouteDto(
            RouteName: "Sample Route",
            Points:
            [
                new RoutePointDto(15.1451, 120.5880),
                new RoutePointDto(15.1458, 120.5895),
                new RoutePointDto(15.1469, 120.5912),
            ]);

        return Ok(sampleRoute);
    }
}

public sealed record RouteDto(string RouteName, IReadOnlyList<RoutePointDto> Points);

public sealed record RoutePointDto(double Latitude, double Longitude);
