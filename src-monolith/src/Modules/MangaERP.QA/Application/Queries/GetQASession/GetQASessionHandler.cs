using MediatR;
using MangaERP.QA.Application.Ports;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetQASession;

public record GetQASessionQuery(Guid ChapterId, Guid RequesterId) : IRequest<QASessionDto?>;

public record QASessionDto(
    Guid Id,
    Guid ChapterId,
    Guid EditorId,
    string Status,
    bool IsApproved,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public class GetQASessionHandler : IRequestHandler<GetQASessionQuery, QASessionDto?>
{
    private readonly IQASessionRepository _repo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetQASessionHandler(
        IQASessionRepository repo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _repo = repo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<QASessionDto?> Handle(GetQASessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        if (session == null) return null;

        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        var isAssignedEditor = chapter.AssignedEditorId == request.RequesterId;
        var isAuthor = series.AuthorId == request.RequesterId;
        var hasActiveSession = session.EditorId == request.RequesterId && session.Status == "InProgress";

        if (!isAssignedEditor && !isAuthor && !hasActiveSession)
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin QA của chương này.");

        return new QASessionDto(
            session.Id,
            session.ChapterId,
            session.EditorId,
            session.Status,
            session.IsApproved,
            session.ApprovedAt,
            session.CreatedAt,
            session.CompletedAt
        );
    }
}
