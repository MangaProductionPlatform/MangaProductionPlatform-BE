using MediatR;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Publishing.Application.Queries.GetPublicationHistory;

public record GetPublicationHistoryQuery(
    Guid SeriesId,
    Guid RequesterId,
    bool CanViewAllPublishingData) : IRequest<IEnumerable<PublicationRecordDto>>;

public record PublicationRecordDto(
    Guid Id,
    Guid ChapterId,
    Guid SeriesId,
    string IssueType,
    string? PublicationUrl,
    DateTime PublishedAt
);

public class GetPublicationHistoryHandler : IRequestHandler<GetPublicationHistoryQuery, IEnumerable<PublicationRecordDto>>
{
    private readonly IPublicationRecordRepository _repo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public GetPublicationHistoryHandler(
        IPublicationRecordRepository repo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo)
    {
        _repo = repo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<PublicationRecordDto>> Handle(GetPublicationHistoryQuery request, CancellationToken cancellationToken)
    {
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} not found.");

        if (!request.CanViewAllPublishingData)
        {
            var isAuthor = series.AuthorId == request.RequesterId;
            var mangaka = await _userRepo.GetByIdAsync(series.AuthorId, cancellationToken);
            var isManagingTantou = mangaka?.ManagingTantouId == request.RequesterId;

            if (!isAuthor && !isManagingTantou)
                throw new UnauthorizedAccessException("Bạn không có quyền xem lịch sử phát hành của series này.");
        }

        var records = await _repo.GetBySeriesIdAsync(request.SeriesId, cancellationToken);

        return records.Select(r => new PublicationRecordDto(
            r.Id,
            r.ChapterId,
            r.SeriesId,
            r.IssueType,
            r.PublicationUrl,
            r.PublishedAt
        )).ToList();
    }
}
