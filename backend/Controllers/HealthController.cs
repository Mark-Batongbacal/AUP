using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace backend.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var startTimestamp = HttpContext.Items["RequestStartTimestamp"] as long? ?? Stopwatch.GetTimestamp();

        return Ok(new
        {
            status = "ok",
            responseTimeMs = Math.Round(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, 2)
        });
    }
}
