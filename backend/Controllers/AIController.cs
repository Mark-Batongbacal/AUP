using backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AIController : ControllerBase
{
    private readonly NemotronAIHelper _aiHelper;

    public AIController(NemotronAIHelper aiHelper)
    {
        _aiHelper = aiHelper;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AIRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty.");

        try
        {
            var response = await _aiHelper.AskAsync(request.Message);

            return Ok(new
            {
                response
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "An error occurred while communicating with the AI.",
                details = ex.Message
            });
        }
    }
}

public class AIRequest
{
    public string Message { get; set; } = string.Empty;
}