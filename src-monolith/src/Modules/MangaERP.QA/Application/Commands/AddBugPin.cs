using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.QA.Application.Commands;

public record AddBugPinCommand(
    Guid ChapterId,
    Guid PageTaskId,
    Guid EditorId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string IssueType,
    string Severity,
    string? Category,
    Guid BatchToken
) : IRequest<Guid>;

public class AddBugPinHandler : IRequestHandler<AddBugPinCommand, Guid>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;

    public AddBugPinHandler(
        IBugPinRepository bugPinRepo, 
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo)
    {
        _bugPinRepo = bugPinRepo;
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
    }

    public async Task<Guid> Handle(AddBugPinCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Chapter state
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.AssignedEditorId != request.EditorId)
            throw new UnauthorizedAccessException("Bạn không phải Tantou Editor được giao cho chương truyện này.");

        if (chapter.Status != ChapterStatus.ReadyForQA && chapter.Status != ChapterStatus.PendingEditorialReview)
            throw new InvalidOperationException("Chỉ có thể thêm ghim lỗi cho chương truyện đang trong trạng thái ReadyForQA.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {request.PageTaskId} not found.");
            
        if (pageTask.ChapterId != request.ChapterId)
            throw new ArgumentException("PageTask này không thuộc về chương truyện đang được QA.");

        // 2. Validate coordinates (0.00% to 100.00%)
        if (request.CoordinateX < 0 || request.CoordinateX > 100 || request.CoordinateY < 0 || request.CoordinateY > 100)
            throw new ArgumentException("Tọa độ X và Y phải nằm trong khoảng từ 0.00 đến 100.00.");

        // 3. Create BugPin
        var bugPin = new BugPin
        {
            ChapterId = request.ChapterId,
            PageTaskId = request.PageTaskId,
            EditorId = request.EditorId,
            CoordinateX = request.CoordinateX,
            CoordinateY = request.CoordinateY,
            NoteMessage = request.NoteMessage.Trim(),
            IssueType = request.IssueType,
            Severity = request.Severity,
            Category = request.Category,
            BatchToken = request.BatchToken,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        await _bugPinRepo.AddAsync(bugPin, cancellationToken);
        return bugPin.Id;
    }
}

public class AddBugPinValidator : AbstractValidator<AddBugPinCommand>
{
    public AddBugPinValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
        RuleFor(x => x.CoordinateX).InclusiveBetween(0m, 100m);
        RuleFor(x => x.CoordinateY).InclusiveBetween(0m, 100m);
        RuleFor(x => x.NoteMessage).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IssueType).Must(x => x == "Visual" || x == "Content" || x == "Text" || x == "Layout")
            .WithMessage("IssueType phải là Visual, Content, Text hoặc Layout.");
        RuleFor(x => x.Severity).Must(x => x == "Low" || x == "Medium" || x == "High" || x == "Critical")
            .WithMessage("Severity phải là Low, Medium, High, hoặc Critical.");
        RuleFor(x => x.BatchToken).NotEmpty();
    }
}
