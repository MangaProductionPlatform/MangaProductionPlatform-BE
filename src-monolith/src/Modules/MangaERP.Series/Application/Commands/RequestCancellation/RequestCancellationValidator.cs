using FluentValidation;

namespace MangaERP.Series.Application.Commands.RequestCancellation;

public class RequestCancellationValidator : AbstractValidator<RequestCancellationCommand>
{
    public RequestCancellationValidator()
    {
        RuleFor(x => x.SeriesId)
            .NotEmpty().WithMessage("SeriesId không được để trống.");

        RuleFor(x => x.RequesterId)
            .NotEmpty().WithMessage("RequesterId không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do hủy không được vượt quá 1000 ký tự.");
    }
}
