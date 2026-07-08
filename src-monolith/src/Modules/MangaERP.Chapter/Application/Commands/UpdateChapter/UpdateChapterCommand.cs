using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.UpdateChapter;

public record UpdateChapterCommand(
    Guid MangakaId,
    Guid ChapterId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    string? CoverImageUrl = null
) : IRequest<UpdateChapterResult>;

public record UpdateChapterResult(
    Guid ChapterId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    string Status,
    string? CoverImageUrl
);

public class UpdateChapterHandler : IRequestHandler<UpdateChapterCommand, UpdateChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public UpdateChapterHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<UpdateChapterResult> Handle(UpdateChapterCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Verify ownership
        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var editorId = chapter.AssignedEditorId;
        if (!editorId.HasValue)
        {
            var mangaka = await _userRepo.GetByIdAsync(cmd.MangakaId, ct);
            editorId = mangaka?.ManagingTantouId;
        }

        // Update metadata using domain method
        chapter.UpdateMetadata(
            cmd.Title,
            cmd.ChapterNumber,
            cmd.TotalPages,
            editorId,
            cmd.CoverImageUrl);

        await _chapterRepo.UpdateAsync(chapter, ct);
        await _chapterRepo.SaveChangesAsync(ct);

        return new UpdateChapterResult(
            chapter.Id,
            chapter.Title,
            chapter.ChapterNumber,
            chapter.TotalPages,
            chapter.Status.ToString(),
            chapter.CoverImageUrl);
    }
}

public class UpdateChapterValidator : AbstractValidator<UpdateChapterCommand>
{
    public UpdateChapterValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ChapterNumber).GreaterThan(0);
        RuleFor(x => x.TotalPages).GreaterThan(0);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048).When(x => x.CoverImageUrl != null);
    }
}
