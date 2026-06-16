using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.UpdateAccount;

public record UpdateAccountCommand(
    Guid UserId,
    string FullName,
    string PersonalEmail,
    UserRole Role,
    string? PhoneNumber = null,
    Guid? ManagingTantouId = null
) : IRequest<UpdateAccountResult>;

public record UpdateAccountResult(
    Guid UserId,
    string Username,
    string FullName,
    string PersonalEmail,
    string Role,
    string? PhoneNumber,
    Guid? ManagingTantouId
);

public class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand, UpdateAccountResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IUsernameGenerator _usernameGenerator;
    private readonly IEmailService _emailService;

    public UpdateAccountHandler(
        IUserRepository userRepo,
        IUsernameGenerator usernameGenerator,
        IEmailService emailService)
    {
        _userRepo = userRepo;
        _usernameGenerator = usernameGenerator;
        _emailService = emailService;
    }

    public async Task<UpdateAccountResult> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        // Validate personal email uniqueness if changed
        if (!string.Equals(user.PersonalEmail, request.PersonalEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepo.PersonalEmailExistsActiveOrPendingAsync(request.PersonalEmail, cancellationToken))
            {
                throw new UserAlreadyExistsException(request.PersonalEmail);
            }
            user.PersonalEmail = request.PersonalEmail;
        }

        // Validate ManagingTantouId if role is Mangaka
        if (request.Role == UserRole.Mangaka && request.ManagingTantouId.HasValue)
        {
            var tantou = await _userRepo.GetByIdAsync(request.ManagingTantouId.Value, cancellationToken);
            if (tantou == null || tantou.Role != UserRole.TantouEditor)
            {
                throw new InvalidOperationException("Biên tập viên phụ trách không hợp lệ hoặc không tồn tại.");
            }
        }

        var usernameChanged = false;
        var oldStatus = user.AccountStatus;

        // If Role or FullName changes, regenerate username
        if (user.Role != request.Role || !string.Equals(user.FullName, request.FullName, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Role == UserRole.Admin || request.Role == UserRole.Reader)
            {
                throw new InvalidOperationException("Không thể gán vai trò Admin hoặc Reader cho tài khoản.");
            }

            var newUsername = await _usernameGenerator.GenerateAsync(request.FullName, request.Role, cancellationToken);
            user.Username = newUsername;
            user.Email = newUsername;
            user.Role = request.Role;
            usernameChanged = true;
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        user.ManagingTantouId = request.Role == UserRole.Mangaka ? request.ManagingTantouId : null;

        await _userRepo.UpdateAsync(user, cancellationToken);

        // Send email if active and username changed
        if (usernameChanged && oldStatus == AccountStatus.Active && !string.IsNullOrWhiteSpace(user.PersonalEmail))
        {
            await _emailService.SendUsernameUpdatedEmailAsync(
                user.PersonalEmail,
                user.Username,
                user.FullName ?? "User",
                cancellationToken);
        }

        return new UpdateAccountResult(
            user.Id,
            user.Username,
            user.FullName ?? string.Empty,
            user.PersonalEmail ?? string.Empty,
            user.Role.ToString(),
            user.PhoneNumber,
            user.ManagingTantouId
        );
    }
}
