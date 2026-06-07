using MangaERP.BuildingBlocks.Domain.Abstractions;

namespace MangaERP.Series.Domain.Entities;

public record SeriesCancelled(Guid EventId, DateTime OccurredOn, Guid SeriesId) : IDomainEvent;
