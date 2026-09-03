using backend.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace backend.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(TukiDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var startTimestamp = HttpContext.Items["RequestStartTimestamp"] as long? ?? Stopwatch.GetTimestamp();

        try
        {
            var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
            var routingDataAvailable = databaseAvailable &&
                await dbContext.TransportRoutes.AsNoTracking().AnyAsync(cancellationToken);

            var response = new
            {
                status = routingDataAvailable ? "ok" : "unhealthy",
                database = databaseAvailable ? "ok" : "unhealthy",
                routingData = routingDataAvailable ? "ok" : "unhealthy",
                responseTimeMs = Math.Round(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, 2)
            };

            return routingDataAvailable
                ? Ok(response)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                database = "unhealthy",
                routingData = "unknown",
                responseTimeMs = Math.Round(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, 2)
            });
        }
    }
}
