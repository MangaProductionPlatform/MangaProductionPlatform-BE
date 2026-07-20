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

namespace MangaERP.Chapter.Application.Commands.ReassignPageTask;

public record ReassignPageTaskCommand(
    Guid MangakaId,
    Guid ChapterId,
    int PageNumber,
    Guid NewAssistantId,
    string? Description = null
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
    private readonly IStudioInvitationRepository _studioRepo;
    private readonly INotificationService _notificationService;

    public ReassignPageTaskHandler(
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

    public async Task<ReassignPageTaskResult> Handle(ReassignPageTaskCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var pageTask = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct)
            ?? throw new KeyNotFoundException($"Page {cmd.PageNumber} not found in chapter {cmd.ChapterId}.");

        var assistant = await _userRepo.GetByIdAsync(cmd.NewAssistantId, ct)
            ?? throw new KeyNotFoundException($"Assistant {cmd.NewAssistantId} not found.");

        if (assistant.Role != UserRole.Assistant)
            throw new InvalidOperationException("Assigned user must have Assistant role.");

        await EnsureAssistantInStudioAsync(series.Id, cmd.NewAssistantId, ct);

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
