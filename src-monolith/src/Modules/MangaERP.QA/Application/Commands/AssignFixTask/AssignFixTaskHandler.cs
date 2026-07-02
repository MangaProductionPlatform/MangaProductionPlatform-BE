using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands.AssignFixTask;

public record AssignFixTaskCommand(Guid PinId, Guid RequesterId, Guid AssistantId, string? Instructions) : IRequest<bool>;

public class AssignFixTaskHandler : IRequestHandler<AssignFixTaskCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPageTaskRepository _pageTaskRepo;

    public AssignFixTaskHandler(
        IBugPinRepository bugPinRepo,
        IPageTaskRepository pageTaskRepo)
    {
        _bugPinRepo = bugPinRepo;
        _pageTaskRepo = pageTaskRepo;
    }

    public async Task<bool> Handle(AssignFixTaskCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể giao sửa lỗi cho pin đang ở trạng thái Open.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(pin.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {pin.PageTaskId} not found.");

        // Update BugPin status to InFixing
        pin.Status = "InFixing";
        await _bugPinRepo.UpdateAsync(pin, cancellationToken);

        // Reopen the page task and assign to the assistant
        pageTask.ReopenForFix(request.AssistantId, request.Instructions);
        await _pageTaskRepo.UpdateAsync(pageTask, cancellationToken);
        await _pageTaskRepo.SaveChangesAsync(cancellationToken);

        return true;
    }
}
