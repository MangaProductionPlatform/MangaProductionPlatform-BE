using MediatR;
using MangaERP.QA.Application.Ports;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.QA.Application.Queries.GetQASession;

public record GetQASessionQuery(Guid ChapterId) : IRequest<QASessionDto?>;

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

    public GetQASessionHandler(IQASessionRepository repo)
    {
        _repo = repo;
    }

    public async Task<QASessionDto?> Handle(GetQASessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        if (session == null) return null;

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
