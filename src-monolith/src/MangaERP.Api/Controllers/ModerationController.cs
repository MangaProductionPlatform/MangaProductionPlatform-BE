using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/moderation")]
[Authorize(Roles = "Admin,EditorialBoard,EditorInChief")]
public class ModerationController : ControllerBase
{
    // Mock endpoints for Frontend integration. Real implementation requires Moderation domain entities.

    [HttpGet("queue")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public IActionResult GetQueue()
    {
        var mockQueue = new List<object>
        {
            new { id = Guid.NewGuid(), type = "Comment", reason = "Spam", status = "Pending", createdAt = DateTime.UtcNow.AddDays(-1) },
            new { id = Guid.NewGuid(), type = "Artwork", reason = "Inappropriate", status = "Pending", createdAt = DateTime.UtcNow }
        };
        return Ok(mockQueue);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetDetail(Guid id)
    {
        return Ok(new
        {
            id = id,
            type = "Artwork",
            reason = "Inappropriate",
            status = "Pending",
            contentUrl = "https://example.com/mock-image.png",
            reportedBy = Guid.NewGuid(),
            createdAt = DateTime.UtcNow
        });
    }

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(200)]
    public IActionResult Resolve(Guid id, [FromBody] ResolveModerationRequest request)
    {
        return Ok(new { message = $"Moderation item {id} resolved with action {request.Action}." });
    }
}

public record ResolveModerationRequest(string Action, string? Note);
