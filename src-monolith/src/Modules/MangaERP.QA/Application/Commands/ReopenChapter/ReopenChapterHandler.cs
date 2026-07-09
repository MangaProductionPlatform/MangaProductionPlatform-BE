using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using FluentValidation;

namespace MangaERP.QA.Application.Commands.ReopenChapter;

public record ReopenChapterCommand(Guid ChapterId, Guid EditorId) : IRequest<ReopenChapterResult>;

public record ReopenChapterResult(Guid ChapterId, string Status, Guid NewQaSessionId, DateTime ReopenedAt);

public class ReopenChapterHandler : IRequestHandler<ReopenChapterCommand, ReopenChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IQASessionRepository _qaSessionRepo;

    public ReopenChapterHandler(IChapterRepository chapterRepo, IQASessionRepository qaSessionRepo)
    {
        _chapterRepo = chapterRepo;
        _qaSessionRepo = qaSessionRepo;
    }

    public async Task<ReopenChapterResult> Handle(ReopenChapterCommand request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.AssignedEditorId != request.EditorId)
            throw new UnauthorizedAccessException("Bạn không phải Tantou Editor được giao cho chương truyện này.");

        // chapter.Reopen() checks if status is Approved and sets it to ReadyForQA
        chapter.Reopen();
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        // Create new QA Session
        var newSession = new QASession
        {
            ChapterId = chapter.Id,
            EditorId = request.EditorId,
            Status = "InProgress",
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };
        await _qaSessionRepo.AddAsync(newSession, cancellationToken);

        return new ReopenChapterResult(chapter.Id, chapter.Status.ToString(), newSession.Id, DateTime.UtcNow);
    }
}

public class ReopenChapterValidator : AbstractValidator<ReopenChapterCommand>
{
    public ReopenChapterValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
