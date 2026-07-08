using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetLayerVersions;

public record GetLayerVersionsQuery(Guid PageTaskId, string LayerType) : IRequest<IEnumerable<LayerVersionDto>>;

public record LayerVersionDto(
    Guid LayerId,
    int Version,
    string FileUrlOriginal,
    string FileUrlOptimized,
    bool IsCurrentVersion,
    DateTime CreatedAt);

public class GetLayerVersionsHandler : IRequestHandler<GetLayerVersionsQuery, IEnumerable<LayerVersionDto>>
{
    private readonly IArtworkLayerRepository _layerRepo;

    public GetLayerVersionsHandler(IArtworkLayerRepository layerRepo) => _layerRepo = layerRepo;

    public async Task<IEnumerable<LayerVersionDto>> Handle(GetLayerVersionsQuery query, CancellationToken ct)
    {
        var layers = await _layerRepo.GetByPageTaskIdAsync(query.PageTaskId, ct);
        
        var filtered = layers
            .Where(l => l.LayerType.Equals(query.LayerType, StringComparison.OrdinalIgnoreCase))
            .Select(l => new LayerVersionDto(
                l.Id,
                l.Version,
                l.FileUrlOriginal,
                l.FileUrlOptimized,
                l.IsCurrentVersion,
                l.CreatedAt))
            .OrderByDescending(l => l.Version);

        return filtered;
    }
}
