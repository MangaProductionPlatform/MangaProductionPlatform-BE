using FluentValidation;
using MangaERP.Shared.Application.Contracts.Events;
using MangaERP.Shared.Application.Contracts.Queries;
using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace MangaERP.Segmentation.Application.Commands.CreateSegmentationTask;

public record CreateSegmentationTaskCommand(
    Guid PageId,
    string MaskRle,
    int[] Bbox,
    SegmentationTaskType TaskType,
    string? Note,
    Guid AssignedToUserId,
    string? AssignedToUserRole,
    Guid CreatedByUserId
) : IRequest<CreateSegmentationTaskResult>;

public record CreateSegmentationTaskResult(Guid TaskId, string Status);

public class CreateSegmentationTaskHandler
    : IRequestHandler<CreateSegmentationTaskCommand, CreateSegmentationTaskResult>
{
    private readonly ISegmentationTaskRepository _repo;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateSegmentationTaskHandler> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateSegmentationTaskHandler(
        ISegmentationTaskRepository repo,
        IMediator mediator,
        ILogger<CreateSegmentationTaskHandler> logger,
        IHttpClientFactory httpClientFactory)
    {
        _repo = repo;
        _mediator = mediator;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected virtual async Task<(int Width, int Height)?> GetImageDimensionsAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(stream, ct);
                if (imageInfo != null)
                {
                    return (imageInfo.Width, imageInfo.Height);
                }
            }
            else
            {
                if (System.IO.File.Exists(imageUrl))
                {
                    using var stream = System.IO.File.OpenRead(imageUrl);
                    var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(stream, ct);
                    if (imageInfo != null)
                    {
                        return (imageInfo.Width, imageInfo.Height);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image dimensions from URL/path: {Url}", imageUrl);
        }
        return null;
    }

    public async Task<CreateSegmentationTaskResult> Handle(
        CreateSegmentationTaskCommand cmd,
        CancellationToken ct)
    {
        int? originalWidth = null;
        int? originalHeight = null;

        var imageUrl = await _mediator.Send(new GetPageTaskPreviewUrlQuery(cmd.PageId), ct);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            var dims = await GetImageDimensionsAsync(imageUrl, ct);
            if (dims.HasValue)
            {
                var (w, h) = dims.Value;
                if (w <= 0 || w > 10000 || h <= 0 || h > 10000)
                {
                    throw new FluentValidation.ValidationException(new[]
                    {
                        new FluentValidation.Results.ValidationFailure("OriginalWidth/OriginalHeight", "Image dimensions are invalid (must be between 1 and 10000).")
                    });
                }
                originalWidth = w;
                originalHeight = h;
            }
        }

        var task = new SegmentationTask
        {
            PageId             = cmd.PageId,
            MaskRle            = cmd.MaskRle,
            Bbox               = cmd.Bbox,
            TaskType           = cmd.TaskType,
            Note               = cmd.Note,
            AssignedToUserId   = cmd.AssignedToUserId,
            AssignedToUserRole = cmd.AssignedToUserRole,
            CreatedByUserId    = cmd.CreatedByUserId,
            Status             = SegmentationTaskStatus.Pending,
            CreatedAt          = DateTime.UtcNow,
            OriginalWidth      = originalWidth,
            OriginalHeight     = originalHeight
        };

        await _repo.AddAsync(task, ct);
        await _repo.SaveChangesAsync(ct);

        try
        {
            await _mediator.Publish(new SegmentationTaskAssignedEvent(
                task.Id,
                task.AssignedToUserId,
                task.CreatedByUserId,
                task.PageId,
                task.TaskType.ToString()), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Segmentation] Failed to publish SegmentationTaskAssignedEvent for task {TaskId}. Task was saved successfully.",
                task.Id);
        }

        return new CreateSegmentationTaskResult(task.Id, task.Status.ToString());
    }
}

public class CreateSegmentationTaskValidator : AbstractValidator<CreateSegmentationTaskCommand>
{
    public CreateSegmentationTaskValidator()
    {
        RuleFor(x => x.PageId).NotEmpty()
            .WithMessage("PageId is required.");

        RuleFor(x => x.MaskRle).NotEmpty()
            .WithMessage("MaskRle is required and must not be empty.");

        RuleFor(x => x.AssignedToUserId).NotEmpty()
            .WithMessage("AssignedToUserId is required.");

        RuleFor(x => x.CreatedByUserId).NotEmpty()
            .WithMessage("CreatedByUserId is required.");

        RuleFor(x => x.TaskType).IsInEnum()
            .WithMessage("TaskType must be a valid SegmentationTaskType value.");

        RuleFor(x => x.Bbox)
            .Must(b => b is { Length: 4 })
            .WithMessage("Bbox must have exactly 4 elements [x1, y1, x2, y2].")
            .When(x => x.Bbox is { Length: > 0 });
    }
}
