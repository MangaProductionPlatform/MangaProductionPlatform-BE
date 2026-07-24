using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
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
    private readonly IUserRepository _userRepo;
    private readonly ICollaborationAuthorizationService _collaborationAuth;
    private readonly INotificationService _notificationService;

    public ActivatePageTaskHandler(
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

    public async Task<ActivatePageTaskResult> Handle(ActivatePageTaskCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageTask = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct)
            ?? throw new KeyNotFoundException($"Page {cmd.PageNumber} not found in chapter {cmd.ChapterId}.");

        var assistant = await _userRepo.GetByIdAsync(cmd.AssignedAssistantId, ct)
            ?? throw new KeyNotFoundException($"Assistant {cmd.AssignedAssistantId} not found.");

        if (assistant.Role != UserRole.Assistant)
            throw new InvalidOperationException("Assigned user must have Assistant role.");

        if (assistant.DeadlineWarningCount >= 3)
            throw new InvalidOperationException("Assistant has been penalized due to too many deadline violations and cannot be assigned to new tasks.");

        if (!await _collaborationAuth.CanReceiveNewAssignmentsAsync(series.AuthorId, series.Id, cmd.AssignedAssistantId, ct))
            throw new InvalidOperationException("Assistant must have an active collaboration and accepted series scope before assignment.");

        if (cmd.BackupAssistantId.HasValue)
        {
            if (cmd.BackupAssistantId.Value == cmd.AssignedAssistantId)
                throw new InvalidOperationException("Backup assistant cannot be the same as primary assistant.");

            var backup = await _userRepo.GetByIdAsync(cmd.BackupAssistantId.Value, ct)
                ?? throw new KeyNotFoundException($"Backup Assistant {cmd.BackupAssistantId} not found.");

            if (backup.Role != UserRole.Assistant)
                throw new InvalidOperationException("Backup user must have Assistant role.");

            if (!await _collaborationAuth.CanReceiveNewAssignmentsAsync(series.AuthorId, series.Id, cmd.BackupAssistantId.Value, ct))
                throw new InvalidOperationException("Backup assistant must have an active collaboration.");
        }

        if (!Enum.TryParse<PageTaskType>(cmd.TaskType, ignoreCase: true, out var taskType))
            throw new InvalidOperationException($"Invalid TaskType '{cmd.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<PageTaskType>())}");

        pageTask.TaskType = taskType;
        pageTask.AssignPrimaryAndBackup(cmd.AssignedAssistantId, cmd.BackupAssistantId, cmd.Description, cmd.Deadline);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        await _notificationService.NotifyTaskAssignedAsync(
            cmd.AssignedAssistantId, pageTask.Id, pageTask.PageNumber, ct);

        return new ActivatePageTaskResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.AssignedAssistantId!.Value,
            pageTask.TaskStatus.ToString(),
            pageTask.Description,
            pageTask.Deadline,
            pageTask.BackupAssistantId);
    }
}

public class ActivatePageTaskValidator : AbstractValidator<ActivatePageTaskCommand>
{
    public ActivatePageTaskValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.AssignedAssistantId).NotEmpty();
        RuleFor(x => x.TaskType).NotEmpty()
            .Must(t => Enum.TryParse<PageTaskType>(t, ignoreCase: true, out _))
            .WithMessage($"TaskType must be one of: {string.Join(", ", Enum.GetNames<PageTaskType>())}");
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Deadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Deadline must be in the future.");
    }
}
