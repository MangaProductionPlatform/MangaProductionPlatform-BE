using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Studio.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
public class LegacyStudioController : ControllerBase
{
    /// <summary>
    /// [Legacy Route - Disabled] Mangaka legacy route viewing unassigned assistants pool.
    /// Returns 410 Gone as unassigned assistant pool is strictly restricted to Admin role.
    /// </summary>
    [HttpGet("api/v1/mangakas/me/unassigned-assistants")]
    [HttpGet("api/v1/studios/unassigned-assistants")]
    [HttpGet("api/v1/studio/unassigned-assistants")]
    [ProducesResponseType(410)]
    public IActionResult DisabledMangakaUnassignedPool()
    {
        return StatusCode(410, new
        {
            code = "ROUTE_DEPRECATED_AND_DISABLED",
            message = "Unassigned assistant pool is restricted to Admin role. Access via this endpoint has been disabled."
        });
    }
}
