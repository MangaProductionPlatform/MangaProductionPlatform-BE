using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.ReassignPageTask;

public record ReassignPageTaskCommand(
    Guid MangakaId,
    Guid ChapterId,
    int PageNumber,
    Guid NewAssistantId,
    string? Description = null,
    bool ConfirmIfSubmitted = false
) : IRequest<ReassignPageTaskResult>;

public record ReassignPageTaskResult(
    Guid PageTaskId,
    int PageNumber,
    Guid AssignedAssistantId,
    string TaskStatus,
    string? Description);

public class ReassignPageTaskHandler : IRequestHandler<ReassignPageTaskCommand, ReassignPageTaskResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICollaborationAuthorizationService _collaborationAuth;
    private readonly INotificationService _notificationService;

    public ReassignPageTaskHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo,
        ICollaborationAuthorizationService collaborationAuth,
        INotificationService notificationService)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
        _collaborationAuth = collaborationAuth;
        _notificationService = notificationService;
    }

    public async Task<ReassignPageTaskResult> Handle(ReassignPageTaskCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageTask = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct)
            ?? throw new KeyNotFoundException($"Page {cmd.PageNumber} not found in chapter {cmd.ChapterId}.");

        if (!cmd.ConfirmIfSubmitted)
        {
            var hasSubmissions = await _pageTaskRepo.HasSubmissionsAsync(pageTask.Id, ct);
            if (hasSubmissions)
            {
                throw new InvalidOperationException("SUBMISSION_EXISTS_CONFIRMATION_REQUIRED");
            }
        }

        var assistant = await _userRepo.GetByIdAsync(cmd.NewAssistantId, ct)
            ?? throw new KeyNotFoundException($"Assistant {cmd.NewAssistantId} not found.");

        if (assistant.Role != UserRole.Assistant)
            throw new InvalidOperationException("Assigned user must have Assistant role.");

        if (assistant.DeadlineWarningCount >= 3)
            throw new InvalidOperationException("Assistant has been penalized due to too many deadline violations and cannot be assigned to new tasks.");

        if (!await _collaborationAuth.CanReceiveNewAssignmentsAsync(series.AuthorId, series.Id, cmd.NewAssistantId, ct))
            throw new InvalidOperationException("Assistant must have an active collaboration and accepted series scope before assignment.");

        pageTask.Reassign(cmd.NewAssistantId, cmd.Description);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        await _notificationService.NotifyTaskAssignedAsync(
            cmd.NewAssistantId, pageTask.Id, pageTask.PageNumber, ct);

        return new ReassignPageTaskResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.AssignedAssistantId!.Value,
            pageTask.TaskStatus.ToString(),
            pageTask.Description);
    }
}

public class ReassignPageTaskValidator : AbstractValidator<ReassignPageTaskCommand>
{
    public ReassignPageTaskValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.NewAssistantId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}
