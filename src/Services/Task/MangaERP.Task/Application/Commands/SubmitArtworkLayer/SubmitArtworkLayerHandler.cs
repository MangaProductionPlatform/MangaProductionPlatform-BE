using MediatR;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;

namespace MangaERP.Task.Application.Commands.SubmitArtworkLayer;

/// <summary>
/// MF2 Step 4: Assistant uploads a transparent artwork layer for a page.
/// Implements MF7 versioning: archive old layer, create new current version.
/// </summary>
public record SubmitArtworkLayerCommand(
    Guid PageTaskId,
    Guid AssistantId,
    LayerType LayerType,
    string FileUrlOriginal,
    string FileUrlOptimized) : IRequest<SubmitArtworkLayerResult>;

public record SubmitArtworkLayerResult(Guid LayerId, int Version);

public class SubmitArtworkLayerHandler : IRequestHandler<SubmitArtworkLayerCommand, SubmitArtworkLayerResult>
{
    private readonly IArtworkLayerRepository _layerRepository;

    public SubmitArtworkLayerHandler(IArtworkLayerRepository layerRepository)
        => _layerRepository = layerRepository;

    public async Task<SubmitArtworkLayerResult> Handle(SubmitArtworkLayerCommand request, CancellationToken cancellationToken)
    {
        // MF7: Archive existing current version
        var existingLayer = await _layerRepository.GetCurrentByPageTaskAsync(request.PageTaskId, cancellationToken);
        int newVersion = 1;
        if (existingLayer is not null)
        {
            existingLayer.IsCurrentVersion = false;
            await _layerRepository.UpdateAsync(existingLayer, cancellationToken);
            newVersion = existingLayer.Version + 1;
        }

        // Create new versioned layer
        var layer = new ArtworkLayer
        {
            PageTaskId = request.PageTaskId,
            AssistantId = request.AssistantId,
            LayerType = request.LayerType,
            FileUrlOriginal = request.FileUrlOriginal,
            FileUrlOptimized = request.FileUrlOptimized,
            Version = newVersion,
            IsCurrentVersion = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _layerRepository.AddAsync(layer, cancellationToken);
        return new SubmitArtworkLayerResult(layer.Id, layer.Version);
    }
}
