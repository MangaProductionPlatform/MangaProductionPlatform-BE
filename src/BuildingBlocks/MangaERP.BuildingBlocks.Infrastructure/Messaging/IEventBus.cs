namespace MangaERP.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Abstraction for publishing integration events to the message bus (RabbitMQ via MassTransit).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}
