using MediatR;
using MangaERP.Publishing.Application.Queries.GetMyNotifications;
using MangaERP.Publishing.Application.Commands.MarkNotificationRead;
using MangaERP.Publishing.Application.Commands.MarkAllNotificationsRead;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace MangaERP.Publishing.Presentation.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// [All roles] Get own notifications. ?unreadOnly=true to filter unread.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), 200)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = new GetMyNotificationsQuery(GetUserId(), unreadOnly);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [All roles] Mark a single notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new MarkNotificationReadCommand(id, GetUserId()), ct);
            return Ok(new { message = "Đã đánh dấu đã đọc." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    /// <summary>
    /// [All roles] Mark ALL unread notifications of the current user as read in one shot.
    /// Useful for the "Mark all as read" button in the notification panel.
    /// Returns the number of notifications that were updated.
    /// </summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var updatedCount = await _mediator.Send(
            new MarkAllNotificationsReadCommand(GetUserId()), ct);

        return Ok(new
        {
            message = $"Đã đánh dấu đọc {updatedCount} thông báo.",
            updatedCount
        });
    }
}
