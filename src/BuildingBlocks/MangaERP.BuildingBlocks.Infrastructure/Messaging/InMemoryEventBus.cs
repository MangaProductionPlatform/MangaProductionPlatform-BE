using System;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.BuildingBlocks.Infrastructure.Messaging;

public class InMemoryEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        Console.WriteLine($"[InMemoryEventBus] Published integration event: {integrationEvent.GetType().Name}");
        return Task.CompletedTask;
    }
}
