using MediatR;
using MangaERP.Publishing.Application.Queries.GetMyNotifications;
using MangaERP.Publishing.Application.Commands.MarkNotificationRead;
using MangaERP.Publishing.Application.Commands.MarkAllNotificationsRead;
using MangaERP.Publishing.Application.Ports;
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
    private readonly IMediator              _mediator;
    private readonly INotificationRepository _notificationRepo;

    public NotificationsController(IMediator mediator, INotificationRepository notificationRepo)
    {
        _mediator         = mediator;
        _notificationRepo = notificationRepo;
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
    /// [All roles] Đếm số thông báo chưa đọc — dùng cho badge Navbar.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var count = await _notificationRepo.CountUnreadAsync(GetUserId(), ct);
        return Ok(new { unreadCount = count });
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

    /// <summary>
    /// [All roles] Xóa một thông báo cụ thể của user đang đăng nhập.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken ct)
    {
        try
        {
            await _notificationRepo.DeleteAsync(id, GetUserId(), ct);
            return Ok(new { message = "Đã xóa thông báo." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// [All roles] Xóa tất cả thông báo đã đọc của user đang đăng nhập (bulk cleanup).
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> DeleteAllRead(CancellationToken ct)
    {
        var deletedCount = await _notificationRepo.DeleteAllReadAsync(GetUserId(), ct);
        return Ok(new
        {
            message = $"Đã xóa {deletedCount} thông báo đã đọc.",
            deletedCount
        });
    }
}

