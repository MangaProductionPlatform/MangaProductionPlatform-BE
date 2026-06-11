using FluentValidation;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Commands.ProvisionAccount;

/// <summary>
/// Validates admin account provisioning input before handler execution.
/// </summary>
public class ProvisionAccountValidator : AbstractValidator<ProvisionAccountCommand>
{
    private static readonly UserRole[] AllowedRoles =
    {
        UserRole.EditorialBoard, UserRole.TantouEditor,
        UserRole.Mangaka, UserRole.Assistant
    };

    public ProvisionAccountValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.")
            .Must(name => name.Trim().Split(' ').Length >= 2)
                .WithMessage("Please provide at least first and last name (e.g. 'Nguyễn Văn Anh').");

        RuleFor(x => x.PersonalEmail)
            .NotEmpty().WithMessage("Personal email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Role)
            .Must(role => AllowedRoles.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles.Select(r => r.ToString()))}. Reader role cannot be provisioned.");
    }
}
