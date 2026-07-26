using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Commands.CheckHalfwayDeadlineWarnings;

public record CheckHalfwayDeadlineWarningsCommand() : IRequest<int>;

public class CheckHalfwayDeadlineWarningsHandler : IRequestHandler<CheckHalfwayDeadlineWarningsCommand, int>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notifications;

    public CheckHalfwayDeadlineWarningsHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        INotificationService notifications)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _notifications = notifications;
    }

    public async Task<int> Handle(CheckHalfwayDeadlineWarningsCommand request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        var activeTasks = (await _taskRepo.GetAllAsync(ct))
            .Where(t => t.TaskStatus == PageTaskStatus.Incomplete
                        && t.WorkStartedAt.HasValue
                        && t.Deadline.HasValue
                        && t.HalfwayWarningSentAt == null)
            .ToList();

        int warningCount = 0;

        foreach (var task in activeTasks)
        {
            var totalSeconds = (task.Deadline!.Value - task.WorkStartedAt!.Value).TotalSeconds;
            if (totalSeconds <= 0) continue;

            var elapsedSeconds = (now - task.WorkStartedAt.Value).TotalSeconds;
            double progressPercent = (elapsedSeconds / totalSeconds) * 100.0;

            if (progressPercent >= 50.0)
            {
                var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);
                if (chapter != null)
                {
                    var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct);
                    if (series != null)
                    {
                        await _notifications.NotifyCollaborationEventAsync(
                            series.AuthorId,
                            "HalfwayDeadlineWarning",
                            "50% Deadline Warning",
                            $"Task #{task.PageNumber} in chapter '{chapter.Title}' has reached 50% of its deadline window.",
                            task.Id,
                            ct);
                    }
                }

                task.HalfwayWarningSentAt = now;
                await _taskRepo.UpdateAsync(task, ct);
                warningCount++;
            }
        }

        if (warningCount > 0)
        {
            await _taskRepo.SaveChangesAsync(ct);
        }

        return warningCount;
    }
}
