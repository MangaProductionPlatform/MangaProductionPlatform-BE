using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.BulkActivatePageTasks;

public class BulkActivatePageTasksHandler : IRequestHandler<BulkActivatePageTasksCommand, BulkActivatePageTasksResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public BulkActivatePageTasksHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<BulkActivatePageTasksResult> Handle(BulkActivatePageTasksCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageNumbers = cmd.PageNumbers.Distinct().ToList();
        var pageTasks = await _pageTaskRepo.GetByChapterAndPageNumbersAsync(cmd.ChapterId, pageNumbers, ct);
        var pageTasksDict = pageTasks.ToDictionary(p => p.PageNumber);

        if (!Enum.TryParse<PageTaskType>(cmd.TaskType, ignoreCase: true, out var taskType))
            throw new InvalidOperationException($"Invalid TaskType '{cmd.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<PageTaskType>())}");

        var results = new List<BulkPageTaskActivationResult>();

        foreach (var pageNum in pageNumbers)
        {
            if (!pageTasksDict.TryGetValue(pageNum, out var pageTask))
                throw new KeyNotFoundException($"Page {pageNum} not found in chapter {cmd.ChapterId}.");

            pageTask.TaskType = taskType;
            pageTask.Description = cmd.Description ?? pageTask.Description;
            pageTask.Deadline = cmd.Deadline ?? pageTask.Deadline;
            pageTask.TaskStatus = PageTaskStatus.Pending;
            pageTask.WorkStartedAt = null;
            pageTask.AssignedAssistantId = null;
            pageTask.PrimaryAssistantId = null;
            pageTask.BackupAssistantId = null;
            pageTask.CurrentAssignmentAttemptId = null;

            await _pageTaskRepo.UpdateAsync(pageTask, ct);

            results.Add(new BulkPageTaskActivationResult(
                pageTask.Id,
                pageTask.PageNumber,
                pageTask.TaskStatus.ToString()
            ));
        }

        await _pageTaskRepo.SaveChangesAsync(ct);

        return new BulkActivatePageTasksResult(chapter.Id, cmd.AssignedAssistantId, results);
    }
}
