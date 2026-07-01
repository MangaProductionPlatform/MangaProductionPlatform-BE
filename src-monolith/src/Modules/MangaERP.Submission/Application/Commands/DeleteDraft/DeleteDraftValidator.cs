using FluentValidation;

namespace MangaERP.Submission.Application.Commands.DeleteDraft;

public class DeleteDraftValidator : AbstractValidator<DeleteDraftCommand>
{
    public DeleteDraftValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("SubmissionId không được để trống.");

        RuleFor(x => x.RequesterId)
            .NotEmpty().WithMessage("RequesterId không được để trống.");
    }
}
