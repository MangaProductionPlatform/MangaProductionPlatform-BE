using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MediatR;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;

namespace MangaERP.Chapter.Application.Commands.CreateChapter;

public record CreateChapterCommand(
    Guid MangakaId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    string? CoverImageUrl = null
) : IRequest<CreateChapterResult>;

public record CreateChapterResult(
    Guid ChapterId,
    string Title,
    decimal ChapterNumber,
    string Status,
    string? CoverImageUrl);

public class CreateChapterHandler : IRequestHandler<CreateChapterCommand, CreateChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public CreateChapterHandler(IChapterRepository chapterRepo, ISeriesRepository seriesRepo, IUserRepository userRepo)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<CreateChapterResult> Handle(CreateChapterCommand cmd, CancellationToken ct)
    {
        var series = await _seriesRepo.GetByIdAsync(cmd.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {cmd.SeriesId} not found.");

        if (series.AuthorId != cmd.MangakaId)
            throw new UnauthorizedAccessException("You do not own this series.");

        if (series.Status != SeriesStatus.Active)
            throw new InvalidOperationException("Chapters can only be created for Active series.");

        var mangaka = await _userRepo.GetByIdAsync(cmd.MangakaId, ct)
            ?? throw new KeyNotFoundException($"Mangaka {cmd.MangakaId} not found.");

        var chapter = ChapterEntity.Create(
            cmd.SeriesId,
            cmd.Title,
            cmd.ChapterNumber,
            cmd.TotalPages,
            mangaka.ManagingTantouId,
            cmd.CoverImageUrl);

        await _chapterRepo.AddAsync(chapter, ct);
        await _chapterRepo.SaveChangesAsync(ct);

        return new CreateChapterResult(
            chapter.Id,
            chapter.Title,
            chapter.ChapterNumber,
            chapter.Status.ToString(),
            chapter.CoverImageUrl);
    }
}

public class CreateChapterValidator : AbstractValidator<CreateChapterCommand>
{
    public CreateChapterValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.SeriesId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ChapterNumber).GreaterThan(0);
        RuleFor(x => x.TotalPages).GreaterThan(0);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048).When(x => x.CoverImageUrl != null);
    }
}
