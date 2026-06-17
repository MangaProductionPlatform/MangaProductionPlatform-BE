using MediatR;
using MangaERP.QA.Application.Commands;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using System.Threading;

namespace MangaERP.Publishing.Application.EventHandlers;

public class ChapterApprovedHandler : INotificationHandler<ChapterApprovedNotification>
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public ChapterApprovedHandler(
        INotificationRepository notificationRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _notificationRepo = notificationRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async System.Threading.Tasks.Task Handle(ChapterApprovedNotification notification, CancellationToken cancellationToken)
    {
        // 1. Get Chapter
        var chapter = await _chapterRepo.GetByIdAsync(notification.ChapterId, cancellationToken);
        if (chapter is null) return;

        // 2. Get Series
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken);
        if (series is null) return;

        // 3. Create Notification for Mangaka
        var dbNotification = new Notification
        {
            ReceiverId = series.AuthorId,
            Title = "Chapter Approved by QA",
            Message = $"Chương truyện '{chapter.Title}' (Chương số {chapter.ChapterNumber}) đã được Tantou Editor phê duyệt chất lượng thành công.",
            IsRead = false,
            NotifyType = "QA_Approved",
            RelatedEntityId = chapter.Id,
            RelatedEntityType = "Chapter",
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(dbNotification, cancellationToken);
    }
}
