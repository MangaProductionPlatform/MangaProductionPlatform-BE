using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.UpdateTaskDeadline;

public record UpdateTaskDeadlineCommand(
    Guid MangakaId,
    Guid PageTaskId,
    DateTime? Deadline
) : IRequest<UpdateTaskDeadlineResult>;

public record UpdateTaskDeadlineResult(
    Guid PageTaskId,
    DateTime? Deadline,
    DateTime UpdatedAt
);

public class UpdateTaskDeadlineHandler : IRequestHandler<UpdateTaskDeadlineCommand, UpdateTaskDeadlineResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public UpdateTaskDeadlineHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<UpdateTaskDeadlineResult> Handle(UpdateTaskDeadlineCommand cmd, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Chỉ Mangaka sở hữu series mới được cập nhật deadline
        if (series.AuthorId != cmd.MangakaId)
            throw new UnauthorizedAccessException("Only the author of the series can update task deadlines.");

        pageTask.SetDeadline(cmd.Deadline);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new UpdateTaskDeadlineResult(
            pageTask.Id,
            pageTask.Deadline,
            pageTask.UpdatedAt
        );
    }
}

public class UpdateTaskDeadlineValidator : AbstractValidator<UpdateTaskDeadlineCommand>
{
    public UpdateTaskDeadlineValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.Deadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Deadline must be in the future.");
    }
}
