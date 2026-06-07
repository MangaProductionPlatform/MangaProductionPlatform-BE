using MediatR;
using MangaERP.Submission.Application.Ports;

namespace MangaERP.Submission.Application.Commands.RequestRevision;

public record RequestRevisionCommand(Guid SubmissionId, Guid ReviewerBoardUserId, string FeedbackMessage) : IRequest;

public class RequestRevisionHandler : IRequestHandler<RequestRevisionCommand>
{
    private readonly ISubmissionRepository _repository;

    public RequestRevisionHandler(ISubmissionRepository repository) => _repository = repository;

    public async Task Handle(RequestRevisionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdAsync(request.SubmissionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Submission {request.SubmissionId} not found.");
        submission.RequestRevision(request.ReviewerBoardUserId, request.FeedbackMessage);
        await _repository.UpdateAsync(submission, cancellationToken);
    }
}
