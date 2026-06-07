using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Commands.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Email,
    string Password,
    string? FullName,
    UserRole Role = UserRole.Reader) : IRequest<RegisterUserResult>;

public record RegisterUserResult(Guid UserId, string Username, string Email, string Role);

public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;

    public RegisterUserHandler(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsAsync(request.Email, cancellationToken))
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        return new RegisterUserResult(user.Id, user.Username, user.Email, user.Role.ToString());
    }
}
