using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Task.Application.Commands.HandleExtensionRequest;

public record HandleExtensionRequestCommand(
    Guid MangakaId,
    Guid RequestId,
    bool IsApproved,
    string? RejectionReason = null
) : IRequest<HandleExtensionRequestResult>;

public record HandleExtensionRequestResult(
    Guid RequestId,
    string Status,
    DateTime? NewDeadline);

public class HandleExtensionRequestHandler : IRequestHandler<HandleExtensionRequestCommand, HandleExtensionRequestResult>
{
    private readonly IDeadlineExtensionRequestRepository _extensionRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notificationService;

    public HandleExtensionRequestHandler(
        IDeadlineExtensionRequestRepository extensionRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        INotificationService notificationService)
    {
        _extensionRepo = extensionRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _notificationService = notificationService;
    }

    public async Task<HandleExtensionRequestResult> Handle(HandleExtensionRequestCommand cmd, CancellationToken ct)
    {
        var request = await _extensionRepo.GetByIdAsync(cmd.RequestId, ct)
            ?? throw new KeyNotFoundException($"Extension request {cmd.RequestId} not found.");

        if (request.Status != "Pending")
            throw new InvalidOperationException("This extension request has already been handled.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {request.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series not found.");

        if (series.AuthorId != cmd.MangakaId)
            throw new UnauthorizedAccessException("Only the author of the series can approve/reject extension requests.");

        if (cmd.IsApproved)
        {
            request.Status = "Approved";
            pageTask.SetDeadline(request.RequestedDeadline);
            await _pageTaskRepo.UpdateAsync(pageTask, ct);
        }
        else
        {
            request.Status = "Rejected";
            request.RejectionReason = cmd.RejectionReason;
        }

        request.HandledAt = DateTime.UtcNow;
        await _extensionRepo.SaveChangesAsync(ct);

        // Gửi thông báo cho Assistant về kết quả phê duyệt
        await _notificationService.NotifyExtensionRequestHandledAsync(
            request.AssistantId,
            pageTask.Id,
            pageTask.PageNumber,
            cmd.IsApproved,
            cmd.RejectionReason,
            cmd.IsApproved ? (DateTime?)request.RequestedDeadline : null,
            ct);

        // Lưu thay đổi cả ở PageTask
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new HandleExtensionRequestResult(
            request.Id,
            request.Status,
            cmd.IsApproved ? (DateTime?)request.RequestedDeadline : null);
    }
}

public class HandleExtensionRequestValidator : AbstractValidator<HandleExtensionRequestCommand>
{
    public HandleExtensionRequestValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .When(x => !x.IsApproved)
            .WithMessage("Rejection reason is required when rejecting the request.")
            .MaximumLength(1000);
    }
}
