using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Commands.ActivatePageTask;

/// <summary>
/// MF2 Step 2: Mangaka activates a page and assigns an assistant.
/// </summary>
public record ActivatePageTaskCommand(
    Guid ChapterId,
    int PageNumber,
    Guid AssignedAssistantId) : IRequest<ActivatePageTaskResult>;

public record ActivatePageTaskResult(Guid PageTaskId, int PageNumber, string Status);

public class ActivatePageTaskHandler : IRequestHandler<ActivatePageTaskCommand, ActivatePageTaskResult>
{
    private readonly IPageTaskRepository _pageTaskRepository;

    public ActivatePageTaskHandler(IPageTaskRepository pageTaskRepository)
        => _pageTaskRepository = pageTaskRepository;

    public async Task<ActivatePageTaskResult> Handle(ActivatePageTaskCommand request, CancellationToken cancellationToken)
    {
        var existingTask = (await _pageTaskRepository.GetByChapterIdAsync(request.ChapterId, cancellationToken))
            .FirstOrDefault(pt => pt.PageNumber == request.PageNumber);

        PageTask pageTask;
        if (existingTask is not null)
        {
            existingTask.TaskStatus = PageTaskStatus.Incomplete;
            existingTask.AssignedAssistantId = request.AssignedAssistantId;
            existingTask.UpdatedAt = DateTime.UtcNow;
            await _pageTaskRepository.UpdateAsync(existingTask, cancellationToken);
            pageTask = existingTask;
        }
        else
        {
            pageTask = new PageTask
            {
                ChapterId = request.ChapterId,
                PageNumber = request.PageNumber,
                AssignedAssistantId = request.AssignedAssistantId,
                TaskStatus = PageTaskStatus.Incomplete,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _pageTaskRepository.AddAsync(pageTask, cancellationToken);
        }

        return new ActivatePageTaskResult(pageTask.Id, pageTask.PageNumber, pageTask.TaskStatus.ToString());
    }
}
