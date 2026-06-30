using FluentValidation;
using MediatR;

namespace MangaERP.Task.Application.Commands.BulkReviewLayers;

public record BulkReviewItem(
    Guid PageTaskId,
    bool IsAccepted,
    string? RejectionNote);

public record BulkReviewLayersCommand(
    Guid MangakaId,
    List<BulkReviewItem> Reviews
) : IRequest<BulkReviewLayersResult>;

public record BulkPageReviewResult(
    Guid PageTaskId,
    string TaskStatus,
    string? PreviewCompositeUrl);

public record BulkReviewLayersResult(
    List<BulkPageReviewResult> Results);

public class BulkReviewLayersValidator : AbstractValidator<BulkReviewLayersCommand>
{
    public BulkReviewLayersValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.Reviews)
            .NotEmpty().WithMessage("Reviews list must not be empty.")
            .Must(reviews => reviews != null && reviews.Count > 0)
            .WithMessage("Reviews list must contain at least one item.");

        RuleForEach(x => x.Reviews).ChildRules(review =>
        {
            review.RuleFor(r => r.PageTaskId).NotEmpty();
            review.RuleFor(r => r.RejectionNote)
                .NotEmpty()
                .When(r => !r.IsAccepted)
                .WithMessage("RejectionNote is required when rejecting a layer.");
        });
    }
}
