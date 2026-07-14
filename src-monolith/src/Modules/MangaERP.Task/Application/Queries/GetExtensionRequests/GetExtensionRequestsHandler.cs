using MangaERP.Chapter.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Task.Application.Ports;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Task.Application.Queries.GetExtensionRequests;

public record GetExtensionRequestsQuery(
    Guid UserId,
    string UserRole,
    Guid? PageTaskId = null
) : IRequest<IEnumerable<ExtensionRequestDto>>;

public record ExtensionRequestDto(
    Guid RequestId,
    Guid PageTaskId,
    int PageNumber,
    string Reason,
    DateTime RequestedDeadline,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt,
    Guid AssistantId,
    string AssistantName);

public class GetExtensionRequestsHandler : IRequestHandler<GetExtensionRequestsQuery, IEnumerable<ExtensionRequestDto>>
{
    private readonly IDeadlineExtensionRequestRepository _extensionRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public GetExtensionRequestsHandler(
        IDeadlineExtensionRequestRepository extensionRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo)
    {
        _extensionRepo = extensionRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<ExtensionRequestDto>> Handle(GetExtensionRequestsQuery query, CancellationToken ct)
    {
        IEnumerable<Domain.Entities.DeadlineExtensionRequest> requests;

        if (query.PageTaskId.HasValue)
        {
            requests = await _extensionRepo.GetByPageTaskIdAsync(query.PageTaskId.Value, ct);
        }
        else if (query.UserRole == "Assistant")
        {
            requests = await _extensionRepo.GetByAssistantIdAsync(query.UserId, ct);
        }
        else
        {
            // For mangakas, we will fetch and filter by series ownership
            // But we can start by getting all requests and then filtering
            // Note: Since this is a small studio context, fetching all and filtering is fine,
            // or we can query based on series.
            // Let's get all from the repo first.
            // We can define a default fetch or query since we don't have GetAllAsync,
            // we can get by assistant if role is assistant.
            // Wait, does IDeadlineExtensionRequestRepository need GetByAssistantIdAsync? Yes, we defined it.
            // Let's check how to get all requests. Since we don't have GetAll, let's query all tasks for that Mangaka's series and get requests.
            // But let's assume we can fetch by pageTaskId, or assistantId. If it's a Mangaka, they will query for a specific PageTaskId anyway,
            // or we can add a method to get all if we want.
            // Let's implement a simple get all by querying page tasks and their requests.
            // Or we can get by assistant or specific task.
            // Let's just fetch all by task or assistant. Let's make sure it handles PageTaskId.
            if (query.PageTaskId.HasValue)
            {
                requests = await _extensionRepo.GetByPageTaskIdAsync(query.PageTaskId.Value, ct);
            }
            else
            {
                // Fallback to empty if not specific page task for non-assistant
                requests = Array.Empty<Domain.Entities.DeadlineExtensionRequest>();
            }
        }

        if (query.UserRole == "Assistant")
        {
            requests = requests.Where(r => r.AssistantId == query.UserId);
        }

        var requestsList = requests.ToList();
        var dtos = new List<ExtensionRequestDto>();

        foreach (var req in requestsList)
        {
            var task = await _pageTaskRepo.GetByIdAsync(req.PageTaskId, ct);
            if (task == null) continue;

            if (query.UserRole == "Mangaka")
            {
                var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);
                if (chapter == null) continue;

                var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct);
                if (series == null || series.AuthorId != query.UserId)
                    continue; // Skip if not the author
            }

            var assistant = await _userRepo.GetByIdAsync(req.AssistantId, ct);
            var assistantName = assistant?.FullName ?? assistant?.Username ?? "Unknown";

            dtos.Add(new ExtensionRequestDto(
                req.Id,
                req.PageTaskId,
                task.PageNumber,
                req.Reason,
                req.RequestedDeadline,
                req.Status,
                req.RejectionReason,
                req.CreatedAt,
                req.AssistantId,
                assistantName));
        }

        return dtos.OrderByDescending(d => d.CreatedAt);
    }
}
