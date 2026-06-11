namespace MangaERP.Shared.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class UserAlreadyExistsException : DomainException
{
    public UserAlreadyExistsException(string email)
        : base($"A user with personal email '{email}' is already pending or active.") { }
}

public class InvalidInvitationTokenException : DomainException
{
    public InvalidInvitationTokenException()
        : base("Invitation token is invalid, expired, or already used.") { }
}

public class AccountAlreadyActivatedException : DomainException
{
    public AccountAlreadyActivatedException()
        : base("This account has already been activated.") { }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entity, Guid id)
        : base($"{entity} with id '{id}' was not found.") { }
}
