using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.UpdateTaskDetails;

public record UpdateTaskDetailsCommand(
    Guid MangakaId,
    Guid PageTaskId,
    string? Description,
    DateTime? Deadline,
    string? TaskType,
    string? BaseImageUrl
) : IRequest<UpdateTaskDetailsResult>;

public record UpdateTaskDetailsResult(
    Guid PageTaskId,
    string? Description,
    DateTime? Deadline,
    string TaskType,
    string BaseImageUrl,
    DateTime UpdatedAt
);

public class UpdateTaskDetailsHandler : IRequestHandler<UpdateTaskDetailsCommand, UpdateTaskDetailsResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public UpdateTaskDetailsHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<UpdateTaskDetailsResult> Handle(UpdateTaskDetailsCommand cmd, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Only the author of the series can update task details
        if (series.AuthorId != cmd.MangakaId)
            throw new UnauthorizedAccessException("Only the author of the series can update task details.");

        // Check task status constraints: Cannot modify if assistant already submitted or task is approved
        if (pageTask.TaskStatus == PageTaskStatus.Reviewing || pageTask.TaskStatus == PageTaskStatus.Approved)
            throw new InvalidOperationException("Cannot update task details after assistant has submitted artwork or task is approved.");

        // 1. Update Description
        if (cmd.Description != null)
        {
            pageTask.Description = cmd.Description;
        }

        // 2. Update Deadline
        if (cmd.Deadline != null || cmd.Deadline == null) // Allows clearing or setting new deadline
        {
            pageTask.SetDeadline(cmd.Deadline);
        }

        // 3. Update TaskType
        if (!string.IsNullOrWhiteSpace(cmd.TaskType))
        {
            if (!Enum.TryParse<PageTaskType>(cmd.TaskType, true, out var parsedTaskType))
                throw new ArgumentException($"Invalid TaskType: '{cmd.TaskType}'. Allowed values: General, Background, Shading, Inking, Effect, Coloring.");

            pageTask.TaskType = parsedTaskType;
        }

        // 4. Update BaseImageUrl (Adds version history if changed)
        if (!string.IsNullOrWhiteSpace(cmd.BaseImageUrl) && cmd.BaseImageUrl != pageTask.BaseImageUrl)
        {
            pageTask.UpdateBaseImage(cmd.BaseImageUrl, cmd.MangakaId);
        }

        pageTask.UpdatedAt = DateTime.UtcNow;

        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new UpdateTaskDetailsResult(
            pageTask.Id,
            pageTask.Description,
            pageTask.Deadline,
            pageTask.TaskType.ToString(),
            pageTask.BaseImageUrl,
            pageTask.UpdatedAt
        );
    }
}

public class UpdateTaskDetailsValidator : AbstractValidator<UpdateTaskDetailsCommand>
{
    public UpdateTaskDetailsValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.Deadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Deadline must be in the future.");
        RuleFor(x => x.BaseImageUrl)
            .Must(url => string.IsNullOrEmpty(url) || (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("BaseImageUrl must be an absolute HTTP or HTTPS URL.");
    }
}
