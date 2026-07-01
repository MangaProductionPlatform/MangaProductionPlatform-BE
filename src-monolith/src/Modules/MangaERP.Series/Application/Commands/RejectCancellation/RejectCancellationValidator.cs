using FluentValidation;

namespace MangaERP.Series.Application.Commands.RejectCancellation;

public class RejectCancellationValidator : AbstractValidator<RejectCancellationCommand>
{
    public RejectCancellationValidator()
    {
        RuleFor(x => x.SeriesId)
            .NotEmpty().WithMessage("SeriesId không được để trống.");

        RuleFor(x => x.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId không được để trống.");

        RuleFor(x => x.RejectReason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");
    }
}
