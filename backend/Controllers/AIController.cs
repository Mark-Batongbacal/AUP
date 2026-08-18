using backend.Services.Assistant;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AIController(ITukiAssistantService assistant) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(
        [FromBody] AssistantRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        var response = await assistant.RespondAsync(userId, request, cancellationToken);
        return response.Status == "INVALID_REQUEST" ? BadRequest(response) : Ok(response);
    }
}
