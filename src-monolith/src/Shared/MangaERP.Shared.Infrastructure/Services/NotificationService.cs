using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;

namespace MangaERP.Shared.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IUserRepository _userRepo;

    public NotificationService(INotificationRepository notificationRepo, IUserRepository userRepo)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
    }

    public async System.Threading.Tasks.Task NotifyTaskAssignedAsync(
        Guid assistantId, Guid pageTaskId, int pageNumber, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "New page task assigned",
            Message = $"You have been assigned page {pageNumber}.",
            NotifyType = "TaskAssigned",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyRevisionRequiredAsync(
        Guid assistantId, Guid pageTaskId, string rejectionNote, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "Revision required",
            Message = rejectionNote,
            NotifyType = "RevisionRequired",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyTaskApprovedAsync(
        Guid assistantId, Guid pageTaskId, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "Layer approved",
            Message = "Your artwork layer has been approved.",
            NotifyType = "TaskApproved",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyChapterReadyForQAAsync(
        Guid chapterId, string chapterTitle, CancellationToken ct = default)
    {
        var editors = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
        foreach (var editor in editors)
        {
            await _notificationRepo.AddAsync(new Notification
            {
                ReceiverId = editor.Id,
                Title = "Chapter ready for QA",
                Message = $"Chapter \"{chapterTitle}\" is ready for editorial review.",
                NotifyType = "ChapterReadyForQA",
                RelatedEntityId = chapterId,
                RelatedEntityType = "Chapter"
            }, ct);
        }

        if (editors.Any())
            await _notificationRepo.SaveChangesAsync(ct);
    }
}
