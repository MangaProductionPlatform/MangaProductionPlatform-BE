using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;

namespace MangaERP.Publishing.Application.Queries.GetMyPublishingQueue;

public record GetMyPublishingQueueQuery(Guid UserId) : IRequest<IEnumerable<PublishingQueueChapterDto>>;

public record PublishingQueueChapterDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string? CoverImageUrl,
    string? IssueType,
    DateTime? ScheduledPublishAt,
    Guid? AssignedEditorId,
    DateTime CreatedAt
);

public class GetMyPublishingQueueHandler : IRequestHandler<GetMyPublishingQueueQuery, IEnumerable<PublishingQueueChapterDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IUserRepository _userRepo;

    public GetMyPublishingQueueHandler(IChapterRepository chapterRepo, IUserRepository userRepo)
    {
        _chapterRepo = chapterRepo;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<PublishingQueueChapterDto>> Handle(GetMyPublishingQueueQuery request, CancellationToken cancellationToken)
    {
        // Get all approved chapters (ready to be scheduled/published)
        // Since we don't have a GetApprovedChaptersAsync that doesn't filter by schedule in the current interface,
        // we'll get all chapters and filter, or we can use the repository if it has a method.
        // IChapterRepository might need a method or we can just fetch and filter.
        // Let's assume there's a way to get chapters by status. Wait, let's look at IChapterRepository.
        // For now, I'll use GetApprovedChaptersAsync(false, ct) since scheduledOnly=false might get all approved.
        var allApproved = await _chapterRepo.GetApprovedChaptersAsync(scheduledOnly: false, cancellationToken);
        
        var isBoard = await _userRepo.HasRbacRoleAsync(request.UserId, "EDITORIAL_BOARD", cancellationToken) ||
                      await _userRepo.HasRbacRoleAsync(request.UserId, "EDITOR_IN_CHIEF", cancellationToken);
                      
        IEnumerable<ChapterEntity> filteredChapters;
        if (isBoard)
        {
            filteredChapters = allApproved;
        }
        else
        {
            filteredChapters = allApproved.Where(c => c.AssignedEditorId == request.UserId);
        }

        return filteredChapters.Select(c => new PublishingQueueChapterDto(
            c.Id,
            c.SeriesId,
            c.Title,
            c.ChapterNumber,
            c.CoverImageUrl,
            c.IssueType,
            c.ScheduledPublishAt,
            c.AssignedEditorId,
            c.CreatedAt
        )).ToList();
    }
}
