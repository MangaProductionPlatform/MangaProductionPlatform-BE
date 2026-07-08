using FluentValidation;
using MangaERP.Task.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
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
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public RollbackLayerHandler(
        IArtworkLayerRepository layerRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _layerRepo = layerRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<RollbackLayerResult> Handle(RollbackLayerCommand cmd, CancellationToken ct)
    {
        var task = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {task.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (series.AuthorId != cmd.RequesterId)
        {
            throw new UnauthorizedAccessException("Only the series owner can rollback task layers.");
        }

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
