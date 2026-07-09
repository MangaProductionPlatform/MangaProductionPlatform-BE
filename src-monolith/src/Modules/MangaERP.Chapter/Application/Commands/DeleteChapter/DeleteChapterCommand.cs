using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.DeleteChapter;

public record DeleteChapterCommand(
    Guid MangakaId,
    Guid ChapterId
) : IRequest<Unit>;

public class DeleteChapterHandler : IRequestHandler<DeleteChapterCommand, Unit>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public DeleteChapterHandler(IChapterRepository chapterRepo, ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<Unit> Handle(DeleteChapterCommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Verify ownership
        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        // Delete chapter using domain method
        chapter.Delete();

        await _chapterRepo.UpdateAsync(chapter, ct);
        await _chapterRepo.SaveChangesAsync(ct);

        return Unit.Value;
    }
}

public class DeleteChapterValidator : AbstractValidator<DeleteChapterCommand>
{
    public DeleteChapterValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
    }
}
