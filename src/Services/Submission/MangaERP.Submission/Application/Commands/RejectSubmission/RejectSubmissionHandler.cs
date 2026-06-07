using MediatR;
using MangaERP.Submission.Application.Ports;

namespace MangaERP.Submission.Application.Commands.RejectSubmission;

public record RejectSubmissionCommand(Guid SubmissionId, Guid ReviewerBoardUserId, string FeedbackMessage) : IRequest;

public class RejectSubmissionHandler : IRequestHandler<RejectSubmissionCommand>
{
    private readonly ISubmissionRepository _repository;

    public RejectSubmissionHandler(ISubmissionRepository repository) => _repository = repository;

    public async Task Handle(RejectSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdAsync(request.SubmissionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Submission {request.SubmissionId} not found.");
        submission.Reject(request.ReviewerBoardUserId, request.FeedbackMessage);
        await _repository.UpdateAsync(submission, cancellationToken);
    }
}
