using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.BulkActivatePageTasks;

public class BulkActivatePageTasksHandler : IRequestHandler<BulkActivatePageTasksCommand, BulkActivatePageTasksResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;
    private readonly IStudioInvitationRepository _studioRepo;
    private readonly INotificationService _notificationService;

    public BulkActivatePageTasksHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo,
        IStudioInvitationRepository studioRepo,
        INotificationService notificationService)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
        _studioRepo = studioRepo;
        _notificationService = notificationService;
    }

    public async Task<BulkActivatePageTasksResult> Handle(BulkActivatePageTasksCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var assistant = await _userRepo.GetByIdAsync(cmd.AssignedAssistantId, ct)
            ?? throw new KeyNotFoundException($"Assistant {cmd.AssignedAssistantId} not found.");

        if (assistant.Role != UserRole.Assistant)
            throw new InvalidOperationException("Assigned user must have Assistant role.");

        if (assistant.DeadlineWarningCount >= 3)
            throw new InvalidOperationException("Assistant has been penalized due to too many deadline violations and cannot be assigned to new tasks.");

        await EnsureAssistantInStudioAsync(series.Id, cmd.AssignedAssistantId, ct);

        var results = new List<BulkPageTaskActivationResult>();
        var activatedTasks = new List<PageTask>();
        var pageNumbers = cmd.PageNumbers.Distinct().ToList();

        var pageTasks = await _pageTaskRepo.GetByChapterAndPageNumbersAsync(cmd.ChapterId, pageNumbers, ct);
        var pageTasksDict = pageTasks.ToDictionary(p => p.PageNumber);

        foreach (var pageNum in pageNumbers)
        {
            if (!pageTasksDict.TryGetValue(pageNum, out var pageTask))
                throw new KeyNotFoundException($"Page {pageNum} not found in chapter {cmd.ChapterId}.");

            pageTask.Activate(cmd.AssignedAssistantId, cmd.Description, cmd.Deadline);
            await _pageTaskRepo.UpdateAsync(pageTask, ct);
            activatedTasks.Add(pageTask);

            results.Add(new BulkPageTaskActivationResult(
                pageTask.Id,
                pageTask.PageNumber,
                pageTask.TaskStatus.ToString()
            ));
        }

        await _pageTaskRepo.SaveChangesAsync(ct);

        // Notify assistant for all assigned page tasks
        foreach (var pageTask in activatedTasks)
        {
            await _notificationService.NotifyTaskAssignedAsync(
                cmd.AssignedAssistantId, pageTask.Id, pageTask.PageNumber, ct);
        }

        return new BulkActivatePageTasksResult(chapter.Id, cmd.AssignedAssistantId, results);
    }

    private async Task EnsureAssistantInStudioAsync(Guid seriesId, Guid assistantId, CancellationToken ct)
    {
        var invitations = await _studioRepo.GetBySeriesIdAsync(seriesId, ct);
        var isMember = invitations.Any(i =>
            i.AssistantUserId == assistantId &&
            i.Status == StudioInvitationStatus.Accepted);

        if (!isMember)
            throw new InvalidOperationException("Assistant must be invited to the series studio before assignment.");
    }
}
