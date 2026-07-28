using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.ActivatePageTask;

public record ActivatePageTaskCommand(
    Guid MangakaId,
    Guid ChapterId,
    int PageNumber,
    Guid AssignedAssistantId,
    string TaskType,
    string? Description = null,
    DateTime? Deadline = null,
    Guid? BackupAssistantId = null
) : IRequest<ActivatePageTaskResult>;

public record ActivatePageTaskResult(
    Guid PageTaskId,
    int PageNumber,
    Guid AssignedAssistantId,
    string TaskStatus,
    string? Description,
    DateTime? Deadline,
    Guid? BackupAssistantId = null);

public class ActivatePageTaskHandler : IRequestHandler<ActivatePageTaskCommand, ActivatePageTaskResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public ActivatePageTaskHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<ActivatePageTaskResult> Handle(ActivatePageTaskCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageTask = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct)
            ?? throw new KeyNotFoundException($"Page {cmd.PageNumber} not found in chapter {cmd.ChapterId}.");

        if (!Enum.TryParse<PageTaskType>(cmd.TaskType, ignoreCase: true, out var taskType))
            throw new InvalidOperationException($"Invalid TaskType '{cmd.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<PageTaskType>())}");

        Guid? targetAssistantId = cmd.AssignedAssistantId != Guid.Empty
            ? cmd.AssignedAssistantId
            : null;

        pageTask.TaskType = taskType;
        pageTask.Description = cmd.Description ?? pageTask.Description;
        pageTask.Deadline = cmd.Deadline ?? pageTask.Deadline;
        pageTask.TaskStatus = PageTaskStatus.Pending;
        pageTask.WorkStartedAt = null;
        pageTask.AssignedAssistantId = targetAssistantId;
        pageTask.PrimaryAssistantId = targetAssistantId;
        pageTask.BackupAssistantId = null;
        pageTask.CurrentAssignmentAttemptId = null;

        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new ActivatePageTaskResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.AssignedAssistantId ?? Guid.Empty,
            pageTask.TaskStatus.ToString(),
            pageTask.Description,
            pageTask.Deadline,
            null);
    }
}

public class ActivatePageTaskValidator : AbstractValidator<ActivatePageTaskCommand>
{
    public ActivatePageTaskValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.TaskType).NotEmpty();
    }
}
