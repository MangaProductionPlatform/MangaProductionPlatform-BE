using MangaERP.Chapter.Application.Ports;
using MangaERP.Shared.Application.Contracts.Queries;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Chapter.Application.Queries.GetPageTaskPreviewUrl;

public class GetPageTaskPreviewUrlQueryHandler : IRequestHandler<GetPageTaskPreviewUrlQuery, string?>
{
    private readonly IPageTaskRepository _pageTaskRepo;

    public GetPageTaskPreviewUrlQueryHandler(IPageTaskRepository pageTaskRepo)
    {
        _pageTaskRepo = pageTaskRepo;
    }

    public async Task<string?> Handle(GetPageTaskPreviewUrlQuery request, CancellationToken cancellationToken)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageId, cancellationToken);
        if (pageTask == null)
        {
            throw new System.Collections.Generic.KeyNotFoundException($"PageTask {request.PageId} not found.");
        }
        return pageTask.PreviewPage?.CompositeFileUrl;
    }
}
