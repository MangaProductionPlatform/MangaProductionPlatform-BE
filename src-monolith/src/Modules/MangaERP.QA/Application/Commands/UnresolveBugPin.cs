using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands;

public record UnresolveBugPinCommand(Guid PinId, Guid EditorId) : IRequest<bool>;

public class UnresolveBugPinHandler : IRequestHandler<UnresolveBugPinCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;

    public UnresolveBugPinHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<bool> Handle(UnresolveBugPinCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.EditorId != request.EditorId)
            throw new UnauthorizedAccessException("Chỉ người tạo ghim lỗi mới có quyền mở lại lỗi.");

        pin.Status = "Open";
        pin.ResolvedAt = null;
        pin.ResolvedNote = null;
        pin.ReviewedLayerId = null;

        await _bugPinRepo.UpdateAsync(pin, cancellationToken);
        return true;
    }
}

public class UnresolveBugPinValidator : AbstractValidator<UnresolveBugPinCommand>
{
    public UnresolveBugPinValidator()
    {
        RuleFor(x => x.PinId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
