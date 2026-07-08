using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Studio.Application.Commands.RemoveStudioMember;
using MediatR;

namespace MangaERP.Chapter.Application.EventHandlers;

public class StudioMemberRemovedHandler : INotificationHandler<StudioMemberRemovedNotification>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;

    public StudioMemberRemovedHandler(IPageTaskRepository pageTaskRepo, IChapterRepository chapterRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
    }

    public async Task Handle(StudioMemberRemovedNotification notification, CancellationToken ct)
    {
        var assistantTasks = await _pageTaskRepo.GetByAssistantAsync(notification.AssistantId, ct);

        foreach (var task in assistantTasks)
        {
            if (task.TaskStatus != PageTaskStatus.Approved)
            {
                var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);
                if (chapter != null && chapter.SeriesId == notification.SeriesId)
                {
                    task.Revoke();
                    await _pageTaskRepo.UpdateAsync(task, ct);
                }
            }
        }

        await _pageTaskRepo.SaveChangesAsync(ct);
    }
}
