using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Commands.CreateChapter;

public record CreateChapterCommand(
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    Guid? AssignedEditorId) : IRequest<CreateChapterResult>;

public record CreateChapterResult(Guid ChapterId, string Title, decimal ChapterNumber, int TotalPages);

public class CreateChapterHandler : IRequestHandler<CreateChapterCommand, CreateChapterResult>
{
    private readonly IChapterRepository _repository;

    public CreateChapterHandler(IChapterRepository repository) => _repository = repository;

    public async Task<CreateChapterResult> Handle(CreateChapterCommand request, CancellationToken cancellationToken)
    {
        var chapter = MangaERP.Chapter.Domain.Entities.Chapter.Create(
            request.SeriesId,
            request.Title,
            request.ChapterNumber,
            request.TotalPages,
            request.AssignedEditorId);

        await _repository.AddAsync(chapter, cancellationToken);

        return new CreateChapterResult(chapter.Id, chapter.Title, chapter.ChapterNumber, chapter.TotalPages);
    }
}
