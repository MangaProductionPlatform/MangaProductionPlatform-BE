using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.AddBasePage;

public record AddBasePageCommand(
    Guid MangakaId,
    Guid ChapterId,
    int PageNumber,
    string BaseImageUrl
) : IRequest<AddBasePageResult>;

public record AddBasePageResult(Guid PageTaskId, int PageNumber, string BaseImageUrl, string TaskStatus);

public class AddBasePageHandler : IRequestHandler<AddBasePageCommand, AddBasePageResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public AddBasePageHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<AddBasePageResult> Handle(AddBasePageCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        if (chapter.Status != ChapterStatus.Draft && chapter.Status != ChapterStatus.QaRevisionRequired)
            throw new InvalidOperationException("Pages can only be added to Draft or QaRevisionRequired chapters.");

        if (cmd.PageNumber > chapter.TotalPages)
            throw new InvalidOperationException($"PageNumber cannot exceed TotalPages ({chapter.TotalPages}).");

        var existing = await _pageTaskRepo.GetByChapterAndPageNumberAsync(cmd.ChapterId, cmd.PageNumber, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Page {cmd.PageNumber} already exists in this chapter.");

        var pageTask = PageTask.CreatePending(cmd.ChapterId, cmd.PageNumber, cmd.BaseImageUrl);
        await _pageTaskRepo.AddAsync(pageTask, ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new AddBasePageResult(
            pageTask.Id,
            pageTask.PageNumber,
            pageTask.BaseImageUrl,
            pageTask.TaskStatus.ToString());
    }
}

public class AddBasePageValidator : AbstractValidator<AddBasePageCommand>
{
    public AddBasePageValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.BaseImageUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .WithMessage("BaseImageUrl must be an absolute HTTP or HTTPS URL.");
    }
}
