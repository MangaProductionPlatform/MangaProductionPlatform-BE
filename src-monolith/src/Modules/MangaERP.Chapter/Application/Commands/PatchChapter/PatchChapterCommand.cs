using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.PatchChapter;

public record PatchChapterCommand(
    Guid MangakaId,
    Guid ChapterId,
    string? Title = null,
    decimal? ChapterNumber = null,
    int? TotalPages = null,
    string? CoverImageUrl = null
) : IRequest<PatchChapterResult>;

public record PatchChapterResult(
    Guid ChapterId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    string Status,
    string? CoverImageUrl,
    Guid? AssignedEditorId
);

public class PatchChapterHandler : IRequestHandler<PatchChapterCommand, PatchChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public PatchChapterHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<PatchChapterResult> Handle(PatchChapterCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Verify ownership
        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        // Merge properties
        var newTitle = cmd.Title ?? chapter.Title;
        var newChapterNumber = cmd.ChapterNumber ?? chapter.ChapterNumber;
        var newTotalPages = cmd.TotalPages ?? chapter.TotalPages;
        var newCoverImageUrl = cmd.CoverImageUrl ?? chapter.CoverImageUrl;

        var newAssignedEditorId = chapter.AssignedEditorId;
        if (!newAssignedEditorId.HasValue)
        {
            var mangaka = await _userRepo.GetByIdAsync(cmd.MangakaId, ct);
            newAssignedEditorId = mangaka?.ManagingTantouId;
        }

        // Update metadata using domain method
        chapter.UpdateMetadata(
            newTitle,
            newChapterNumber,
            newTotalPages,
            newAssignedEditorId,
            newCoverImageUrl);

        await _chapterRepo.UpdateAsync(chapter, ct);
        await _chapterRepo.SaveChangesAsync(ct);

        return new PatchChapterResult(
            chapter.Id,
            chapter.Title,
            chapter.ChapterNumber,
            chapter.TotalPages,
            chapter.Status.ToString(),
            chapter.CoverImageUrl,
            chapter.AssignedEditorId);
    }
}

public class PatchChapterValidator : AbstractValidator<PatchChapterCommand>
{
    public PatchChapterValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256).When(x => x.Title != null);
        RuleFor(x => x.ChapterNumber).GreaterThan(0).When(x => x.ChapterNumber != null);
        RuleFor(x => x.TotalPages).GreaterThan(0).When(x => x.TotalPages != null);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048).When(x => x.CoverImageUrl != null);
    }
}
