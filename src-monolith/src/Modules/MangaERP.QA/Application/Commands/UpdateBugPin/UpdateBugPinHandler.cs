using MediatR;
using MangaERP.QA.Application.Ports;
using FluentValidation;

namespace MangaERP.QA.Application.Commands.UpdateBugPin;

public record UpdateBugPinCommand(
    Guid PinId,
    Guid EditorId,
    string? NoteMessage,
    string? IssueType,
    decimal? CoordinateX,
    decimal? CoordinateY,
    string? Severity,
    string? Category
) : IRequest<bool>;

public class UpdateBugPinHandler : IRequestHandler<UpdateBugPinCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;

    public UpdateBugPinHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<bool> Handle(UpdateBugPinCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        if (pin.EditorId != request.EditorId)
            throw new UnauthorizedAccessException("Chỉ người tạo ghim lỗi mới có quyền chỉnh sửa.");

        if (pin.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa ghim lỗi đang ở trạng thái Open (chưa gửi feedback).");

        if (request.NoteMessage != null) pin.NoteMessage = request.NoteMessage.Trim();
        if (request.IssueType != null) pin.IssueType = request.IssueType;
        if (request.CoordinateX.HasValue) pin.CoordinateX = request.CoordinateX.Value;
        if (request.CoordinateY.HasValue) pin.CoordinateY = request.CoordinateY.Value;
        if (request.Severity != null) pin.Severity = request.Severity;
        if (request.Category != null) pin.Category = request.Category;

        await _bugPinRepo.UpdateAsync(pin, cancellationToken);
        return true;
    }
}

public class UpdateBugPinValidator : AbstractValidator<UpdateBugPinCommand>
{
    public UpdateBugPinValidator()
    {
        RuleFor(x => x.PinId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
        RuleFor(x => x.CoordinateX).InclusiveBetween(0m, 100m).When(x => x.CoordinateX.HasValue);
        RuleFor(x => x.CoordinateY).InclusiveBetween(0m, 100m).When(x => x.CoordinateY.HasValue);
        RuleFor(x => x.NoteMessage).MaximumLength(1000).When(x => x.NoteMessage != null);
        RuleFor(x => x.IssueType).Must(x => x == "Visual" || x == "Content" || x == "Text" || x == "Layout")
            .When(x => x.IssueType != null)
            .WithMessage("IssueType phải là Visual, Content, Text hoặc Layout.");
        RuleFor(x => x.Severity).Must(x => x == "Low" || x == "Medium" || x == "High" || x == "Critical")
            .When(x => x.Severity != null)
            .WithMessage("Severity phải là Low, Medium, High, hoặc Critical.");
    }
}
