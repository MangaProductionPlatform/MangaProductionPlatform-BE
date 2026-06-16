using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.ActivatePageTask;

public record ActivatePageTaskCommand(
    Guid MangakaId,
    Guid ChapterId,
    int PageNumber,
    Guid AssignedAssistantId
) : IRequest<ActivatePageTaskResult>;

public record ActivatePageTaskResult(
    Guid PageTaskId,
    int PageNumber,
    Guid AssignedAssistantId,
    string TaskStatus);

public class ActivatePageTaskHandler : IRequestHandler<ActivatePageTaskCommand, ActivatePageTaskResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;
    private readonly IStudioInvitationRepository _studioRepo;
    private readonly INotificationService _notificationService;

    public ActivatePageTaskHandler(
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

        await EnsureAssistantInStudioAsync(series.Id, cmd.AssignedAssistantId, ct);

        pageTask.Activate(cmd.AssignedAssistantId);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        await _notificationService.NotifyTaskAssignedAsync(
            cmd.AssignedAssistantId, pageTask.Id, pageTask.PageNumber, ct);

        return new ActivatePageTaskResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.AssignedAssistantId!.Value,
            pageTask.TaskStatus.ToString());
    }

    private async Task EnsureAssistantInStudioAsync(Guid seriesId, Guid assistantId, CancellationToken ct)
    {
        var invitations = await _studioRepo.GetBySeriesIdAsync(seriesId, ct);
        var isMember = invitations.Any(i =>
            i.AssistantUserId == assistantId &&
            (i.Status == StudioInvitationStatus.Accepted ||
             (i.IsNewAccountFlow && i.Status == StudioInvitationStatus.Pending)));

        if (!isMember)
            throw new InvalidOperationException("Assistant must be invited to the series studio before assignment.");
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
    }
}
