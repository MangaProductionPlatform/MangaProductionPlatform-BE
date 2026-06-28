using MediatR;
using MangaERP.Publishing.Application.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Publishing.Application.Queries.GetPublicationHistory;

public record GetPublicationHistoryQuery(Guid SeriesId) : IRequest<IEnumerable<PublicationRecordDto>>;

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

    public GetPublicationHistoryHandler(IPublicationRecordRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<PublicationRecordDto>> Handle(GetPublicationHistoryQuery request, CancellationToken cancellationToken)
    {
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
