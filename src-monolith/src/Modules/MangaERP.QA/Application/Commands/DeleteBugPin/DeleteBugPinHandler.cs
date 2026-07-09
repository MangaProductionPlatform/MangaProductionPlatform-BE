using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands.DeleteBugPin;

public record DeleteBugPinCommand(Guid PinId, Guid EditorId) : IRequest<bool>;

public class DeleteBugPinHandler : IRequestHandler<DeleteBugPinCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;

    public DeleteBugPinHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<bool> Handle(DeleteBugPinCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.EditorId != request.EditorId)
            throw new UnauthorizedAccessException("Chỉ người tạo ghim lỗi mới có quyền xóa.");

        if (pin.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể xóa ghim lỗi đang ở trạng thái Open (chưa gửi feedback).");

        await _bugPinRepo.DeleteAsync(pin, cancellationToken);
        return true;
    }
}
