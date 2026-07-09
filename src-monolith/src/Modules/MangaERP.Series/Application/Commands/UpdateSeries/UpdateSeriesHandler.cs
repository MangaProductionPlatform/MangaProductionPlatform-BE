using FluentValidation;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Commands.UpdateSeries;

public record UpdateSeriesCommand(
    Guid SeriesId,
    Guid AuthorId,
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl
) : IRequest<UpdateSeriesResult>;

public record UpdateSeriesResult(
    Guid SeriesId,
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string Status);

public class UpdateSeriesHandler : IRequestHandler<UpdateSeriesCommand, UpdateSeriesResult>
{
    private readonly ISeriesRepository _repo;

    public UpdateSeriesHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<UpdateSeriesResult> Handle(UpdateSeriesCommand cmd, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(cmd.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {cmd.SeriesId} not found.");

        if (series.AuthorId != cmd.AuthorId)
            throw new UnauthorizedAccessException("You are not allowed to update this series.");

        series.UpdateMetadata(cmd.Title, cmd.Description, cmd.Genre, cmd.CoverImageUrl);

        await _repo.SaveChangesAsync(ct);

        return new UpdateSeriesResult(
            series.Id,
            series.Title,
            series.Description,
            series.Genre,
            series.CoverImageUrl,
            series.Status.ToString());
    }
}

public class UpdateSeriesValidator : AbstractValidator<UpdateSeriesCommand>
{
    public UpdateSeriesValidator()
    {
        RuleFor(x => x.SeriesId).NotEmpty();
        RuleFor(x => x.AuthorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048).When(x => x.CoverImageUrl != null);
    }
}
