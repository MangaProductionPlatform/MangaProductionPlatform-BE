using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands.ReportBugPinFixed;

public record ReportBugPinFixedCommand(Guid PinId, Guid UserId) : IRequest<bool>;

public class ReportBugPinFixedHandler : IRequestHandler<ReportBugPinFixedCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;

    public ReportBugPinFixedHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<bool> Handle(ReportBugPinFixedCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.Status != "InFixing")
            throw new InvalidOperationException("Chỉ có thể báo cáo đã sửa lỗi với pin đang ở trạng thái InFixing.");

        pin.Status = "Fixed";
        await _bugPinRepo.UpdateAsync(pin, cancellationToken);

        return true;
    }
}
