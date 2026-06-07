using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Commands.ElevateToMangaka;

/// <summary>
/// Called by the Submission service (via internal API or event) when a submission is approved.
/// Atomically elevates the user's role from Reader to Mangaka.
/// </summary>
public record ElevateToMangakaCommand(Guid UserId) : IRequest<bool>;

public class ElevateToMangakaHandler : IRequestHandler<ElevateToMangakaCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public ElevateToMangakaHandler(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<bool> Handle(ElevateToMangakaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null) return false;

        user.Role = UserRole.Mangaka;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
