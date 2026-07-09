using FluentValidation;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.BulkActivatePageTasks;

public record BulkActivatePageTasksCommand(
    Guid MangakaId,
    Guid ChapterId,
    List<int> PageNumbers,
    Guid AssignedAssistantId,
    string? Description = null,
    DateTime? Deadline = null
) : IRequest<BulkActivatePageTasksResult>;

public record BulkPageTaskActivationResult(
    Guid PageTaskId,
    int PageNumber,
    string TaskStatus);

public record BulkActivatePageTasksResult(
    Guid ChapterId,
    Guid AssignedAssistantId,
    List<BulkPageTaskActivationResult> ActivatedPages);

public class BulkActivatePageTasksValidator : AbstractValidator<BulkActivatePageTasksCommand>
{
    public BulkActivatePageTasksValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumbers)
            .NotEmpty().WithMessage("Page numbers list must not be empty.")
            .Must(pages => pages != null && pages.All(p => p > 0))
            .WithMessage("All page numbers must be greater than 0.");
        RuleFor(x => x.AssignedAssistantId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Deadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Deadline must be in the future.");
    }
}
