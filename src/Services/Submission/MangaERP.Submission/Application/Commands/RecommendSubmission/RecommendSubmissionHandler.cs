using MediatR;
using MangaERP.Submission.Application.Ports;

namespace MangaERP.Submission.Application.Commands.RecommendSubmission;

public record RecommendSubmissionCommand(Guid SubmissionId, Guid EditorId, string RecommendationMessage) : IRequest;

public class RecommendSubmissionHandler : IRequestHandler<RecommendSubmissionCommand>
{
    private readonly ISubmissionRepository _repository;

    public RecommendSubmissionHandler(ISubmissionRepository repository) => _repository = repository;

    public async Task Handle(RecommendSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdAsync(request.SubmissionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Submission {request.SubmissionId} not found.");

        submission.RecommendToBoard(request.EditorId, request.RecommendationMessage);
        await _repository.UpdateAsync(submission, cancellationToken);
    }
}
