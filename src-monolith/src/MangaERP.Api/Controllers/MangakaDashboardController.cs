using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/mangaka")]
[Authorize(Roles = "Mangaka")]
public class MangakaDashboardController : ControllerBase
{
    // Placeholder for Mangaka Dashboard, pulling data from Series, Submission, and Chapter modules

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetDashboard()
    {
        return Ok(new
        {
            activeSeriesCount = 2,
            pendingSubmissionsCount = 1,
            unresolvedQAPins = 5,
            recentNotifications = new List<object>
            {
                new { id = Guid.NewGuid(), message = "Chapter 5 passed QA", date = DateTime.UtcNow.AddDays(-1) }
            },
            seriesAnalytics = new List<object>
            {
                new { seriesId = Guid.NewGuid(), title = "Mock Series", views = 1500, votes = 80 }
            }
        });
    }
}
