using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Identity.Application.Commands.ResendActivation;

public record ResendActivationCommand(
    Guid UserId
) : IRequest;

public class ResendActivationHandler : IRequestHandler<ResendActivationCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IInvitationTokenRepository _invTokenRepo;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public ResendActivationHandler(
        IUserRepository userRepo,
        IInvitationTokenRepository invTokenRepo,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration config)
    {
        _userRepo = userRepo;
        _invTokenRepo = invTokenRepo;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
    }

    public async Task Handle(ResendActivationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (user.AccountStatus != AccountStatus.PendingActivation)
        {
            throw new InvalidOperationException("Chỉ có thể gửi lại link kích hoạt cho tài khoản đang chờ kích hoạt.");
        }

        var personalEmail = user.PersonalEmail;
        if (string.IsNullOrWhiteSpace(personalEmail))
        {
            throw new InvalidOperationException("Tài khoản không có địa chỉ email cá nhân để nhận mã kích hoạt.");
        }

        // Generate a new 24h JWT invitation token
        var jwtToken = _tokenService.GenerateInvitationToken(
            user.Id, personalEmail, user.Username, user.Role.ToString());

        // Persist new invitation token
        var invToken = new InvitationToken
        {
            Token = jwtToken,
            UserId = user.Id,
            PersonalEmail = personalEmail,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        await _invTokenRepo.AddAsync(invToken, cancellationToken);

        // Send activation email
        var baseUrl = _config["Invitation:ActivationBaseUrl"] ?? "https://company.com/activate";
        var activationLink = $"{baseUrl}?token={Uri.EscapeDataString(jwtToken)}";
        await _emailService.SendInvitationEmailAsync(
            personalEmail, activationLink, user.Username, user.FullName ?? string.Empty, cancellationToken);
    }
}
