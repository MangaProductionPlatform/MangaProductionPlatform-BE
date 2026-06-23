using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Identity.Application.Commands.ProvisionAccount;

public record ProvisionAccountCommand(
    string FullName,
    string PersonalEmail,
    UserRole Role,
    string? PhoneNumber = null
) : IRequest<ProvisionAccountResult>;

public record ProvisionAccountResult(
    Guid UserId,
    string GeneratedUsername,
    string PersonalEmail,
    string Role,
    string Status
);

public class ProvisionAccountHandler : IRequestHandler<ProvisionAccountCommand, ProvisionAccountResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IInvitationTokenRepository _invTokenRepo;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUsernameGenerator _usernameGenerator;
    private readonly IConfiguration _config;

    public ProvisionAccountHandler(
        IUserRepository userRepo,
        IInvitationTokenRepository invTokenRepo,
        ITokenService tokenService,
        IEmailService emailService,
        IUsernameGenerator usernameGenerator,
        IConfiguration config)
    {
        _userRepo = userRepo;
        _invTokenRepo = invTokenRepo;
        _tokenService = tokenService;
        _emailService = emailService;
        _usernameGenerator = usernameGenerator;
        _config = config;
    }

    public async Task<ProvisionAccountResult> Handle(ProvisionAccountCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Validate personal email uniqueness
        if (await _userRepo.PersonalEmailExistsActiveOrPendingAsync(request.PersonalEmail, cancellationToken))
            throw new UserAlreadyExistsException(request.PersonalEmail);

        // Step 2: Generate unique corporate username
        var username = await _usernameGenerator.GenerateAsync(request.FullName, request.Role, cancellationToken);

        // Step 3: Create user in PendingActivation state
        var user = new User
        {
            Username = username,
            Email = username,               // corporate email IS the login email
            PersonalEmail = request.PersonalEmail,
            PasswordHash = string.Empty,    // not set until activation
            Role = request.Role,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            ManagingTantouId = null,
            AccountStatus = AccountStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepo.AddAsync(user, cancellationToken);

        // Step 4: Generate 24h JWT invitation token
        var jwtToken = _tokenService.GenerateInvitationToken(
            user.Id, request.PersonalEmail, username, request.Role.ToString());

        // Step 5: Persist invitation token entity
        var invToken = new InvitationToken
        {
            Token = jwtToken,
            UserId = user.Id,
            PersonalEmail = request.PersonalEmail,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        await _invTokenRepo.AddAsync(invToken, cancellationToken);

        // Step 6: Send activation email
        var baseUrl = _config["Invitation:ActivationBaseUrl"] ?? "https://company.com/activate";
        var activationLink = $"{baseUrl}?token={Uri.EscapeDataString(jwtToken)}";
        await _emailService.SendInvitationEmailAsync(
            request.PersonalEmail, activationLink, username, request.FullName, cancellationToken);

        return new ProvisionAccountResult(user.Id, username, request.PersonalEmail,
            request.Role.ToString(), AccountStatus.PendingActivation.ToString());
    }
}
