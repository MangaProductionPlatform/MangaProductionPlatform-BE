using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands;

public record ResolveBugPinCommand(Guid PinId, Guid EditorId) : IRequest<bool>;

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

        if (pin.Status == "Resolved")
            return true;

        pin.Status = "Resolved";
        pin.ResolvedAt = DateTime.UtcNow;

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
