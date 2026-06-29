using FluentValidation;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetLayerHistory;

public record GetLayerHistoryQuery(
    Guid MangakaId,
    Guid? SeriesId = null,
    Guid? ChapterId = null,
    Guid? PageTaskId = null,
    string? Status = null
) : IRequest<IEnumerable<LayerHistoryDto>>;

public record LayerHistoryDto(
    Guid LayerId,
    Guid PageTaskId,
    int PageNumber,
    string LayerType,
    string FileUrlOriginal,
    string FileUrlOptimized,
    int Version,
    bool IsCurrentVersion,
    string? RejectionNote,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string Status);

public class GetLayerHistoryValidator : AbstractValidator<GetLayerHistoryQuery>
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accepted", "Rejected", "Pending", "Current"
    };

    public GetLayerHistoryValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(status => status == null || ValidStatuses.Contains(status))
            .WithMessage("Status must be one of: Accepted, Rejected, Pending, Current.");
    }
}
