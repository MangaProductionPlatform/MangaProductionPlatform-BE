using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands;

public record ResolveBugPinCommand(Guid PinId, Guid EditorId, string? Note = null, Guid? ReviewedLayerId = null) : IRequest<bool>;

public class ResolveBugPinHandler : IRequestHandler<ResolveBugPinCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;

    public ResolveBugPinHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<bool> Handle(ResolveBugPinCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.EditorId != request.EditorId)
            throw new UnauthorizedAccessException("Chỉ người tạo ghim lỗi mới có quyền xác nhận sửa lỗi.");

        pin.Status = "Resolved";
        pin.ResolvedAt = DateTime.UtcNow;
        if (request.Note != null) pin.ResolvedNote = request.Note;
        if (request.ReviewedLayerId != null) pin.ReviewedLayerId = request.ReviewedLayerId;

        await _bugPinRepo.UpdateAsync(pin, cancellationToken);
        return true;
    }
}

public class ResolveBugPinValidator : AbstractValidator<ResolveBugPinCommand>
{
    public ResolveBugPinValidator()
    {
        RuleFor(x => x.PinId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
