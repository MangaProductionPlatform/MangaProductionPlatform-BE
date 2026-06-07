namespace MangaERP.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Marker interface for domain events raised within the domain layer.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
