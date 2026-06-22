using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MangaERP.Shared.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications.
/// Server pushes events to connected clients via IHubContext.
/// Client subscribes to "ReceiveNotification" for incoming notifications.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>
    /// Called when a client connects. Logs connection for debugging.
    /// UserIdentifier is auto-resolved from JWT ClaimTypes.NameIdentifier.
    /// </summary>
    public override async System.Threading.Tasks.Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            // Auto-join a personal notification group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async System.Threading.Tasks.Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
