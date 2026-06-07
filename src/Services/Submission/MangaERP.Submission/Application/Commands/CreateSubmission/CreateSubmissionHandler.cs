using MediatR;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;

namespace MangaERP.Submission.Application.Commands.CreateSubmission;

public record CreateSubmissionCommand(
    Guid SubmitterId,
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string ManuscriptUrl) : IRequest<Guid>;

public class CreateSubmissionHandler : IRequestHandler<CreateSubmissionCommand, Guid>
{
    private readonly ISubmissionRepository _repository;

    public CreateSubmissionHandler(ISubmissionRepository repository) => _repository = repository;

    public async Task<Guid> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = SeriesSubmission.Create(
            request.SubmitterId, request.Title, request.Description,
            request.Genre, request.CoverImageUrl, request.ManuscriptUrl);

        await _repository.AddAsync(submission, cancellationToken);
        return submission.Id;
    }
}
