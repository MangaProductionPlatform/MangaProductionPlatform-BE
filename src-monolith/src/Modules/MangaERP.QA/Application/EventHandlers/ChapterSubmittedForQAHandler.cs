using MediatR;
using MangaERP.Chapter.Application.Commands.SubmitForQA;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using System.Threading;

namespace MangaERP.QA.Application.EventHandlers;

public class ChapterSubmittedForQAHandler : INotificationHandler<ChapterSubmittedForQANotification>
{
    private readonly IQASessionRepository _qaSessionRepo;

    public ChapterSubmittedForQAHandler(IQASessionRepository qaSessionRepo)
    {
        _qaSessionRepo = qaSessionRepo;
    }

    public async System.Threading.Tasks.Task Handle(ChapterSubmittedForQANotification notification, CancellationToken cancellationToken)
    {
        // Check if session already exists for this chapter
        var existingSession = await _qaSessionRepo.GetByChapterIdAsync(notification.ChapterId, cancellationToken);
        if (existingSession is not null)
        {
            // Reset existing session if it was completed
            if (existingSession.Status == "Completed")
            {
                existingSession.Status = "InProgress";
                existingSession.IsApproved = false;
                existingSession.ApprovedAt = null;
                existingSession.CompletedAt = null;
                await _qaSessionRepo.UpdateAsync(existingSession, cancellationToken);
            }
            return;
        }

        // Create new QA session
        var qaSession = new QASession
        {
            ChapterId = notification.ChapterId,
            EditorId = notification.AssignedEditorId ?? Guid.Empty, // Assign editor if available, otherwise empty Guid
            Status = "InProgress",
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        await _qaSessionRepo.AddAsync(qaSession, cancellationToken);
    }
}
