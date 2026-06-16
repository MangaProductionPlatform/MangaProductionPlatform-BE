using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MediatR;

namespace MangaERP.Task.Application.Commands.SubmitArtworkLayer;

public record SubmitArtworkLayerCommand(
    Guid AssistantId,
    Guid PageTaskId,
    string LayerType,
    string FileUrlOriginal,
    string? FileUrlOptimized
) : IRequest<SubmitArtworkLayerResult>;

public record SubmitArtworkLayerResult(
    Guid LayerId,
    int Version,
    string TaskStatus);

public class SubmitArtworkLayerHandler : IRequestHandler<SubmitArtworkLayerCommand, SubmitArtworkLayerResult>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IArtworkLayerRepository _layerRepo;

    public SubmitArtworkLayerHandler(IPageTaskRepository pageTaskRepo, IArtworkLayerRepository layerRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _layerRepo = layerRepo;
    }

    public async Task<SubmitArtworkLayerResult> Handle(SubmitArtworkLayerCommand cmd, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {cmd.PageTaskId} not found.");

        if (!pageTask.CanSubmitLayer(cmd.AssistantId))
            throw new UnauthorizedAccessException("You are not allowed to submit a layer for this page task.");

        await _layerRepo.MarkPreviousVersionsNotCurrentAsync(cmd.PageTaskId, ct);
        var nextVersion = await _layerRepo.GetMaxVersionAsync(cmd.PageTaskId, ct) + 1;

        var layer = ArtworkLayer.Submit(
            cmd.PageTaskId,
            cmd.AssistantId,
            cmd.LayerType,
            cmd.FileUrlOriginal,
            cmd.FileUrlOptimized ?? cmd.FileUrlOriginal,
            nextVersion);

        pageTask.MarkReviewing();

        await _layerRepo.AddAsync(layer, ct);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _layerRepo.SaveChangesAsync(ct);

        return new SubmitArtworkLayerResult(layer.Id, layer.Version, pageTask.TaskStatus.ToString());
    }
}

public class SubmitArtworkLayerValidator : AbstractValidator<SubmitArtworkLayerCommand>
{
    public SubmitArtworkLayerValidator()
    {
        RuleFor(x => x.AssistantId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.LayerType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FileUrlOriginal).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.FileUrlOptimized).MaximumLength(2048).When(x => x.FileUrlOptimized is not null);
    }
}
