using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;
using System.Text.Json;

namespace MangaERP.Chapter.Application.Commands.SetPageRegion;

/// <summary>
/// Saves the SAM-selected region mask and work type for a page task.
/// Called by the frontend after Mangaka clicks to select a region and picks a task type.
/// </summary>
/// <param name="MangakaId">ID of the authenticated Mangaka making the request.</param>
/// <param name="ChapterId">Chapter that owns the page.</param>
/// <param name="PageNumber">Page number within the chapter.</param>
/// <param name="RegionMask">JSON string — SAM mask polygon as array of [x,y] points.</param>
/// <param name="TaskType">Type of artwork work (Background, Shading, etc.).</param>
public record SetPageRegionCommand(
    Guid   MangakaId,
    Guid   ChapterId,
    int    PageNumber,
    string RegionMask,
    string TaskType
) : IRequest<SetPageRegionResult>;

public record SetPageRegionResult(
    Guid   PageTaskId,
    int    PageNumber,
    string TaskType,
    string RegionMask,
    string TaskStatus
);

public class SetPageRegionHandler : IRequestHandler<SetPageRegionCommand, SetPageRegionResult>
{
    private readonly IChapterRepository  _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository   _seriesRepo;

    public SetPageRegionHandler(
        IChapterRepository  chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository   seriesRepo)
    {
        _chapterRepo  = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo   = seriesRepo;
    }

    public async System.Threading.Tasks.Task<SetPageRegionResult> Handle(
        SetPageRegionCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageTask = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct)
            ?? throw new KeyNotFoundException($"Page {cmd.PageNumber} not found in chapter {cmd.ChapterId}.");

        // Parse and validate that RegionMask is valid JSON
        try { JsonDocument.Parse(cmd.RegionMask); }
        catch { throw new InvalidOperationException("RegionMask must be a valid JSON string."); }

        if (!Enum.TryParse<PageTaskType>(cmd.TaskType, ignoreCase: true, out var taskType))
            throw new InvalidOperationException(
                $"Invalid TaskType '{cmd.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<PageTaskType>())}");

        pageTask.SetRegion(cmd.RegionMask, taskType);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new SetPageRegionResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.TaskType.ToString(),
            pageTask.RegionMask!,
            pageTask.TaskStatus.ToString()
        );
    }
}

public class SetPageRegionValidator : AbstractValidator<SetPageRegionCommand>
{
    public SetPageRegionValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.RegionMask).NotEmpty().WithMessage("RegionMask is required.");
        RuleFor(x => x.TaskType).NotEmpty()
            .Must(t => Enum.TryParse<PageTaskType>(t, ignoreCase: true, out _))
            .WithMessage($"TaskType must be one of: {string.Join(", ", Enum.GetNames<PageTaskType>())}");
    }
}
