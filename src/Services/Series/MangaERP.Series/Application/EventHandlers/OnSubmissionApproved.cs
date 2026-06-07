using MangaERP.BuildingBlocks.Contracts.IntegrationEvents;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.EventHandlers;

/// <summary>
/// MassTransit consumer: creates a MangaSeries record when SubmissionApprovedEvent is received from Submission service.
/// Corresponds to MF1 Step 5 (Atomic Process Approval).
/// </summary>
public class OnSubmissionApproved : INotificationHandler<SubmissionApprovedNotification>
{
    // In actual implementation, this would be a MassTransit IConsumer<SubmissionApprovedEvent>
    // For now, we use MediatR notification as a placeholder
    public Task Handle(SubmissionApprovedNotification notification, CancellationToken cancellationToken)
    {
        var series = MangaSeries.Create(
            notification.MangakaUserId,
            notification.SubmissionId,
            notification.SeriesTitle,
            null,
            notification.Genre,
            notification.CoverImageUrl);
        // Repository.AddAsync(series, cancellationToken);
        return Task.CompletedTask;
    }
}

public record SubmissionApprovedNotification(
    Guid SubmissionId,
    Guid MangakaUserId,
    string SeriesTitle,
    string? Genre,
    string? CoverImageUrl) : INotification;
