using FluentValidation;
using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Commands.RollbackLayer;

public record RollbackLayerCommand(
    Guid PageTaskId,
    string LayerType,
    int Version,
    Guid RequesterId
) : IRequest<RollbackLayerResult>;

public record RollbackLayerResult(
    Guid PageTaskId,
    string LayerType,
    int NewCurrentVersion,
    string FileUrlOriginal);

public class RollbackLayerHandler : IRequestHandler<RollbackLayerCommand, RollbackLayerResult>
{
    private readonly IArtworkLayerRepository _layerRepo;

    public RollbackLayerHandler(IArtworkLayerRepository layerRepo) => _layerRepo = layerRepo;

    public async Task<RollbackLayerResult> Handle(RollbackLayerCommand cmd, CancellationToken ct)
    {
        var layers = await _layerRepo.GetByPageTaskIdAsync(cmd.PageTaskId, ct);
        var targetTypeLayers = layers
            .Where(l => l.LayerType.Equals(cmd.LayerType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var targetLayer = targetTypeLayers.FirstOrDefault(l => l.Version == cmd.Version)
            ?? throw new KeyNotFoundException($"Layer version {cmd.Version} for type {cmd.LayerType} not found.");

        foreach (var layer in targetTypeLayers)
        {
            layer.IsCurrentVersion = (layer.Id == targetLayer.Id);
            await _layerRepo.UpdateAsync(layer, ct);
        }

        await _layerRepo.SaveChangesAsync(ct);

        return new RollbackLayerResult(
            cmd.PageTaskId,
            cmd.LayerType,
            targetLayer.Version,
            targetLayer.FileUrlOriginal);
    }
}

public class RollbackLayerValidator : AbstractValidator<RollbackLayerCommand>
{
    public RollbackLayerValidator()
    {
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.LayerType).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
