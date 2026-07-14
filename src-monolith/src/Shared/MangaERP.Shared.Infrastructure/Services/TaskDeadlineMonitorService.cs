using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;

namespace MangaERP.Shared.Infrastructure.Services;

public class TaskDeadlineMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskDeadlineMonitorService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public TaskDeadlineMonitorService(
        IServiceProvider serviceProvider,
        ILogger<TaskDeadlineMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task Deadline Monitor Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during task deadline monitoring check.");
            }

            await System.Threading.Tasks.Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Task Deadline Monitor Service is stopping.");
    }

    private async System.Threading.Tasks.Task MonitorDeadlinesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        // Lấy tất cả PageTask chưa hoàn thành (Incomplete hoặc RevisionAlert) và có đặt Deadline
        var activeTasks = await db.PageTasks
            .Where(t => t.Deadline != null &&
                        (t.TaskStatus == PageTaskStatus.Incomplete || t.TaskStatus == PageTaskStatus.RevisionAlert) &&
                        t.AssignedAssistantId != null)
            .ToListAsync(ct);

        foreach (var task in activeTasks)
        {
            var assistantId = task.AssignedAssistantId!.Value;
            var deadline = task.Deadline!.Value;

            // 1. Cảnh báo sắp đến hạn (trước 3 ngày)
            if (deadline - now <= TimeSpan.FromDays(3) && deadline >= now)
            {
                var hasWarned3Days = await db.Notifications.AnyAsync(n =>
                    n.ReceiverId == assistantId &&
                    n.RelatedEntityId == task.Id &&
                    n.NotifyType == "TaskDeadlineWarning3Days", ct);

                if (!hasWarned3Days)
                {
                    _logger.LogInformation("Task {TaskId} is due in 3 days. Sending warning to assistant {AssistantId}.", task.Id, assistantId);
                    await notificationService.NotifyTaskDeadline3DaysAsync(assistantId, task.Id, task.PageNumber, deadline, ct);
                }
            }

            // 2. Cảnh báo trễ hạn (Overdue)
            if (deadline < now)
            {
                var hasWarnedOverdue = await db.Notifications.AnyAsync(n =>
                    n.ReceiverId == assistantId &&
                    n.RelatedEntityId == task.Id &&
                    n.NotifyType == "TaskDeadlineOverdueWarning", ct);

                if (!hasWarnedOverdue)
                {
                    _logger.LogInformation("Task {TaskId} has missed the deadline. Warning assistant {AssistantId}.", task.Id, assistantId);
                    
                    var assistant = await db.Users.FirstOrDefaultAsync(u => u.Id == assistantId, ct);
                    if (assistant != null)
                    {
                        assistant.DeadlineWarningCount += 1;
                        await notificationService.NotifyTaskOverdueWarningAsync(assistantId, task.Id, task.PageNumber, ct);

                        // Phạt nếu đạt từ 3 lần cảnh báo trễ deadline trở lên
                        if (assistant.DeadlineWarningCount >= 3)
                        {
                            var hasPenalizedNotification = await db.Notifications.AnyAsync(n =>
                                n.ReceiverId == assistantId &&
                                n.NotifyType == "AssistantPenalized", ct);

                            if (!hasPenalizedNotification)
                            {
                                await notificationService.NotifyAssistantPenalizedAsync(assistantId, assistant.DeadlineWarningCount, ct);
                            }
                        }

                        await db.SaveChangesAsync(ct);
                    }
                }
            }
        }
    }
}
