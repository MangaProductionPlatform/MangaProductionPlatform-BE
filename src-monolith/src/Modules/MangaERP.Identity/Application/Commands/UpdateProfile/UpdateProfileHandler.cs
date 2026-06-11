using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string? PenName,
    string[]? DrawingSoftwares,
    string? BankAccountNumber
) : IRequest;

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand>
{
    private readonly IUserRepository _userRepo;

    public UpdateProfileHandler(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (request.PenName != null)
        {
            user.PenName = string.IsNullOrWhiteSpace(request.PenName) ? null : request.PenName.Trim();
        }

        if (request.DrawingSoftwares != null)
        {
            user.DrawingSoftwares = request.DrawingSoftwares.Length == 0 
                ? null 
                : string.Join(",", request.DrawingSoftwares);
        }

        if (request.BankAccountNumber != null)
        {
            user.BankAccountNumber = string.IsNullOrWhiteSpace(request.BankAccountNumber) ? null : request.BankAccountNumber.Trim();
        }

        await _userRepo.UpdateAsync(user, cancellationToken);
    }
}
