using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Identity.Application.Commands.ProvisionAccount;

public record ProvisionAccountCommand(
    string FullName,
    string PersonalEmail,
    UserRole Role,
    string? PhoneNumber = null,
    Guid? ManagingTantouId = null,
    Guid? ManagingMangakaId = null
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
    private readonly IAssistantCollaborationProvisionPort _collabProvisionPort;
    private readonly IDbContextProvider _dbProvider;
    private readonly IConfiguration _config;

    public ProvisionAccountHandler(
        IUserRepository userRepo,
        IInvitationTokenRepository invTokenRepo,
        ITokenService tokenService,
        IEmailService emailService,
        IUsernameGenerator usernameGenerator,
        IAssistantCollaborationProvisionPort collabProvisionPort,
        IDbContextProvider dbProvider,
        IConfiguration config)
    {
        _userRepo = userRepo;
        _invTokenRepo = invTokenRepo;
        _tokenService = tokenService;
        _emailService = emailService;
        _usernameGenerator = usernameGenerator;
        _collabProvisionPort = collabProvisionPort;
        _dbProvider = dbProvider;
        _config = config;
    }

    public async Task<ProvisionAccountResult> Handle(ProvisionAccountCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Validate personal email uniqueness
        if (await _userRepo.PersonalEmailExistsActiveOrPendingAsync(request.PersonalEmail, cancellationToken))
            throw new UserAlreadyExistsException(request.PersonalEmail);

        // Step 1b: Validate mandatory Tantou assignment for Mangaka role
        if (request.Role == UserRole.Mangaka)
        {
            if (!request.ManagingTantouId.HasValue || request.ManagingTantouId.Value == Guid.Empty)
                throw new InvalidOperationException("ManagingTantouId is required when provisioning a Mangaka account.");

            var tantou = await _userRepo.GetByIdAsync(request.ManagingTantouId.Value, cancellationToken);
            if (tantou == null || (tantou.Role != UserRole.TantouEditor && !tantou.UserRoles.Any(ur => ur.Role.Name == RoleNames.TantouEditor)) || tantou.AccountStatus != AccountStatus.Active || tantou.IsDeleted)
                throw new InvalidOperationException("Assigned Tantou Editor must exist, hold TantouEditor role, and be Active.");
        }

        // Step 1c: Validate optional ManagingMangakaId for Assistant role
        User? mangaka = null;
        if (request.Role == UserRole.Assistant && request.ManagingMangakaId.HasValue && request.ManagingMangakaId.Value != Guid.Empty)
        {
            mangaka = await _userRepo.GetByIdAsync(request.ManagingMangakaId.Value, cancellationToken);
            if (mangaka == null || (mangaka.Role != UserRole.Mangaka && !mangaka.UserRoles.Any(ur => ur.Role.Name == RoleNames.Mangaka)) || mangaka.AccountStatus != AccountStatus.Active || mangaka.IsDeleted)
                throw new InvalidOperationException("Assigned Mangaka must exist, hold Mangaka role, and be Active.");
        }

        var db = (DbContext)_dbProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        var (createdUserId, generatedUsername, token) = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                // Step 2: Generate unique corporate username
                var username = await _usernameGenerator.GenerateAsync(request.FullName, request.Role, cancellationToken);

                // Step 3: Create user in PendingActivation state
                var user = new User
                {
                    Username = username,
                    Email = username,               // corporate email IS the login email
                    PersonalEmail = request.PersonalEmail,
                    NormalizedPersonalEmail = request.PersonalEmail.Trim().ToLowerInvariant(),
                    PasswordHash = string.Empty,    // not set until activation
                    Role = request.Role,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    ManagingTantouId = request.Role == UserRole.Mangaka ? request.ManagingTantouId : null,
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

                // Step 5b: Create MangakaAssistantCollaboration if Role == Assistant
                if (request.Role == UserRole.Assistant && mangaka != null)
                {
                    if (await _collabProvisionPort.HasNonEndedCollaborationAsync(user.Id, cancellationToken))
                        throw new InvalidOperationException("Assistant already has a non-ended collaboration.");

                    await _collabProvisionPort.CreateActiveCollaborationAsync(mangaka.Id, user.Id, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);

                return (user.Id, username, jwtToken);
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        // Step 6: Send activation email (outside retry strategy & transaction)
        var baseUrl = _config["Invitation:ActivationBaseUrl"] ?? "https://company.com/activate";
        var activationLink = $"{baseUrl}?token={Uri.EscapeDataString(token)}";
        await _emailService.SendInvitationEmailAsync(
            request.PersonalEmail, activationLink, generatedUsername, request.FullName, cancellationToken);

        return new ProvisionAccountResult(createdUserId, generatedUsername, request.PersonalEmail,
            request.Role.ToString(), AccountStatus.PendingActivation.ToString());
    }
}
