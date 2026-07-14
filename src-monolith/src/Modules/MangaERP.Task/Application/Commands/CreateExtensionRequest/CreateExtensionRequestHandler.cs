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

namespace MangaERP.Task.Application.Commands.CreateExtensionRequest;

public record CreateExtensionRequestCommand(
    Guid AssistantId,
    Guid PageTaskId,
    string Reason,
    DateTime RequestedDeadline
) : IRequest<CreateExtensionRequestResult>;

public record CreateExtensionRequestResult(
    Guid RequestId,
    Guid PageTaskId,
    string Status,
    DateTime RequestedDeadline);

public class CreateExtensionRequestHandler : IRequestHandler<CreateExtensionRequestCommand, CreateExtensionRequestResult>
{
    private readonly IDeadlineExtensionRequestRepository _extensionRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notificationService;

    public CreateExtensionRequestHandler(
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

    public async Task<CreateExtensionRequestResult> Handle(CreateExtensionRequestCommand cmd, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {cmd.PageTaskId} not found.");

        if (pageTask.AssignedAssistantId != cmd.AssistantId)
            throw new UnauthorizedAccessException("You are not assigned to this task.");

        if (cmd.RequestedDeadline <= DateTime.UtcNow)
            throw new ArgumentException("Requested deadline must be in the future.");

        var hasPendingRequest = await _extensionRepo.HasPendingRequestAsync(cmd.PageTaskId, ct);
        if (hasPendingRequest)
            throw new InvalidOperationException("There is already a pending extension request for this task.");

        var request = new DeadlineExtensionRequest
        {
            PageTaskId = cmd.PageTaskId,
            AssistantId = cmd.AssistantId,
            Reason = cmd.Reason,
            RequestedDeadline = cmd.RequestedDeadline,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _extensionRepo.AddAsync(request, ct);

        // Lấy thông tin Mangaka để gửi thông báo
        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct);
        if (chapter != null)
        {
            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct);
            if (series != null)
            {
                await _notificationService.NotifyDeadlineExtensionRequestedAsync(
                    series.AuthorId,
                    request.Id,
                    pageTask.Id,
                    pageTask.PageNumber,
                    cmd.RequestedDeadline,
                    ct);
            }
        }

        await _extensionRepo.SaveChangesAsync(ct);

        return new CreateExtensionRequestResult(
            request.Id,
            request.PageTaskId,
            request.Status,
            request.RequestedDeadline);
    }
}

public class CreateExtensionRequestValidator : AbstractValidator<CreateExtensionRequestCommand>
{
    public CreateExtensionRequestValidator()
    {
        RuleFor(x => x.AssistantId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RequestedDeadline).GreaterThan(DateTime.UtcNow);
    }
}
