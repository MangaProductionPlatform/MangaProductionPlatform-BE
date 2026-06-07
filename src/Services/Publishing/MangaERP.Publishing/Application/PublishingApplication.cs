using MediatorR = MediatR;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.BuildingBlocks.Contracts.IntegrationEvents;
using MangaERP.BuildingBlocks.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using MangaERP.BuildingBlocks.Infrastructure.Persistence;

namespace MangaERP.Publishing.Application;

// ─── Repository Port ─────────────────────────────────────────────────────────
public interface IPublicationRepository
{
    Task<PublicationRecord?> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PublicationRecord>> GetBySeriesIdAsync(Guid seriesId, CancellationToken cancellationToken = default);
    Task AddAsync(PublicationRecord record, CancellationToken cancellationToken = default);
}

// ─── Command: Schedule publication (MF3 Step 6) ──────────────────────────────
public record SchedulePublicationCommand(
    Guid ChapterId,
    Guid SeriesId,
    string IssueType,
    DateTime ScheduledPublishAt) : MediatorR.IRequest;

public class SchedulePublicationHandler : MediatorR.IRequestHandler<SchedulePublicationCommand>
{
    // In a real implementation this would update the Chapter record via an event or direct DB call
    // For now we log it as a pending record
    private readonly IPublicationRepository _repository;

    public SchedulePublicationHandler(IPublicationRepository repository) => _repository = repository;

    public async Task Handle(SchedulePublicationCommand request, CancellationToken cancellationToken)
    {
        // Pre-create a stub publication record with the scheduled time
        var record = new PublicationRecord
        {
            ChapterId = request.ChapterId,
            IssueType = request.IssueType,
            PublicationUrl = string.Empty,  // will be filled by background job on actual publish
            CacheKey = $"chapter:{request.ChapterId}",
            PublishedAt = request.ScheduledPublishAt
        };
        await _repository.AddAsync(record, cancellationToken);
    }
}

// ─── Command: Publish chapter (MF3 Step 7 — called by background job) ────────
public record PublishChapterCommand(
    Guid ChapterId,
    Guid SeriesId,
    string ProductionFileUrl) : MediatorR.IRequest;

public class PublishChapterHandler : MediatorR.IRequestHandler<PublishChapterCommand>
{
    private readonly IPublicationRepository _repository;
    private readonly IEventBus _eventBus;

    public PublishChapterHandler(IPublicationRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(PublishChapterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        string cacheKey = $"chapter:{request.ChapterId}";

        if (existing is not null)
        {
            existing.PublicationUrl = request.ProductionFileUrl;
            existing.PublishedAt = DateTime.UtcNow;
        }
        else
        {
            var record = new PublicationRecord
            {
                ChapterId = request.ChapterId,
                IssueType = "Weekly",
                PublicationUrl = request.ProductionFileUrl,
                CacheKey = cacheKey,
                PublishedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(record, cancellationToken);
        }

        // Publish event — triggers Redis cache invalidation (MF8) and notifications
        var evt = new ChapterPublishedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            request.ChapterId, request.SeriesId,
            request.ProductionFileUrl, cacheKey, DateTime.UtcNow);
        await _eventBus.PublishAsync(evt, cancellationToken);
    }
}

// ─── Query: Get publication history ──────────────────────────────────────────
public record GetPublicationHistoryQuery(Guid SeriesId) : MediatorR.IRequest<IEnumerable<PublicationRecordDto>>;

public record PublicationRecordDto(Guid Id, Guid ChapterId, string IssueType, string PublicationUrl, DateTime PublishedAt);

public class GetPublicationHistoryHandler : MediatorR.IRequestHandler<GetPublicationHistoryQuery, IEnumerable<PublicationRecordDto>>
{
    private readonly IPublicationRepository _repository;

    public GetPublicationHistoryHandler(IPublicationRepository repository) => _repository = repository;

    public async Task<IEnumerable<PublicationRecordDto>> Handle(GetPublicationHistoryQuery request, CancellationToken cancellationToken)
    {
        var records = await _repository.GetBySeriesIdAsync(request.SeriesId, cancellationToken);
        return records.Select(r => new PublicationRecordDto(r.Id, r.ChapterId, r.IssueType, r.PublicationUrl, r.PublishedAt));
    }
}
