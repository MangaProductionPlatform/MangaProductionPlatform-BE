using MediatR;
using MangaERP.Task.Application.Ports;
using MangaERP.BuildingBlocks.Contracts.IntegrationEvents;
using MangaERP.BuildingBlocks.Infrastructure.Messaging;
using Task = System.Threading.Tasks.Task;

namespace MangaERP.Task.Application.Commands.ReviewLayer;

/// <summary>
/// MF2 Step 6: Mangaka accepts or rejects a submitted artwork layer.
/// Acceptance triggers a check for overall chapter completion.
/// </summary>
public record ReviewLayerCommand(
    Guid PageTaskId,
    Guid ReviewerMangakaId,
    bool IsAccepted,
    string? RejectionNote) : IRequest;

public class ReviewLayerHandler : IRequestHandler<ReviewLayerCommand>
{
    private readonly IArtworkLayerRepository _layerRepository;
    private readonly IEventBus _eventBus;

    public ReviewLayerHandler(IArtworkLayerRepository layerRepository, IEventBus eventBus)
    {
        _layerRepository = layerRepository;
        _eventBus = eventBus;
    }

    public async System.Threading.Tasks.Task Handle(ReviewLayerCommand request, CancellationToken cancellationToken)
    {
        var layer = await _layerRepository.GetCurrentByPageTaskAsync(request.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"No current layer found for PageTask {request.PageTaskId}.");


        layer.ReviewedAt = DateTime.UtcNow;

        if (request.IsAccepted)
        {
            // Publish integration event — Chapter service will handle progression
            var evt = new LayerAcceptedEvent(
                Guid.NewGuid(), DateTime.UtcNow,
                request.PageTaskId, layer.Id);
            await _eventBus.PublishAsync(evt, cancellationToken);
        }
        else
        {
            layer.RejectionNote = request.RejectionNote
                ?? throw new ArgumentException("RejectionNote is required when rejecting a layer.");
            await _layerRepository.UpdateAsync(layer, cancellationToken);

            var evt = new LayerRejectedEvent(
                Guid.NewGuid(), DateTime.UtcNow,
                request.PageTaskId, layer.Id, layer.AssistantId, request.RejectionNote);
            await _eventBus.PublishAsync(evt, cancellationToken);
        }
    }
}
