using System.Security.Cryptography;
using System.Text;
using MediatR;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;

namespace MangaERP.Ranking.Application.Commands.ImportRankingCsv;

public record ImportRankingCsvCommand(
    Guid UploaderId,
    string Filename,
    byte[] FileBytes,
    RankingPeriod Period,
    string? PeriodIdentifier = null,
    bool DryRun = false
) : IRequest<ImportRankingCsvResult>;

public record ImportRankingCsvResult(
    bool Success,
    Guid BatchId,
    int TotalRows,
    int SuccessCount,
    int ErrorCount,
    List<string> ValidationErrors,
    bool IsDryRun,
    string Message
);

public class ImportRankingCsvHandler : IRequestHandler<ImportRankingCsvCommand, ImportRankingCsvResult>
{
    private readonly IRankingRepository _rankingRepo;

    public ImportRankingCsvHandler(IRankingRepository rankingRepo)
    {
        _rankingRepo = rankingRepo;
    }

    public async Task<ImportRankingCsvResult> Handle(ImportRankingCsvCommand request, CancellationToken cancellationToken)
    {
        if (request.FileBytes == null || request.FileBytes.Length == 0)
            throw new InvalidOperationException("CSV file content is empty.");

        // Compute SHA256 checksum
        using var sha256 = SHA256.Create();
        var checksumBytes = sha256.ComputeHash(request.FileBytes);
        var checksum = BitConverter.ToString(checksumBytes).Replace("-", "").ToLowerInvariant();

        var csvContent = Encoding.UTF8.GetString(request.FileBytes);
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            throw new InvalidOperationException("CSV file must contain a header row and at least one data row.");

        var header = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var seriesIdIdx = Array.IndexOf(header, "seriesid");
        var rankIdx = Array.IndexOf(header, "rank");
        var scoreIdx = Array.IndexOf(header, "score");
        var viewsIdx = Array.IndexOf(header, "views");
        var likesIdx = Array.IndexOf(header, "likes");
        var favoritesIdx = Array.IndexOf(header, "favorites");
        var commentsIdx = Array.IndexOf(header, "comments");
        var trendScoreIdx = Array.IndexOf(header, "trendscore");

        if (seriesIdIdx < 0 || rankIdx < 0 || scoreIdx < 0)
        {
            throw new InvalidOperationException("CSV header must contain required columns: 'SeriesId', 'Rank', 'Score'.");
        }

        var validationErrors = new List<string>();
        var snapshotsToInsert = new List<RankingSnapshot>();
        var seenSeriesIds = new HashSet<Guid>();
        var seenRanks = new HashSet<int>();

        var seriesIdSet = await _rankingRepo.GetValidSeriesIdsAsync(cancellationToken);

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',').Select(c => c.Trim().Trim('"')).ToArray();
            var rowNum = i + 1;

            if (cols.Length <= Math.Max(seriesIdIdx, Math.Max(rankIdx, scoreIdx)))
            {
                validationErrors.Add($"Row {rowNum}: Insufficient columns.");
                continue;
            }

            if (!Guid.TryParse(cols[seriesIdIdx], out var seriesId))
            {
                validationErrors.Add($"Row {rowNum}: Invalid SeriesId format '{cols[seriesIdIdx]}'.");
                continue;
            }

            if (seriesIdSet.Count > 0 && !seriesIdSet.Contains(seriesId))
            {
                validationErrors.Add($"Row {rowNum}: Series with ID '{seriesId}' does not exist.");
            }

            if (seenSeriesIds.Contains(seriesId))
            {
                validationErrors.Add($"Row {rowNum}: Duplicate SeriesId '{seriesId}'.");
            }
            else
            {
                seenSeriesIds.Add(seriesId);
            }

            if (!int.TryParse(cols[rankIdx], out var rank) || rank <= 0)
            {
                validationErrors.Add($"Row {rowNum}: Rank must be a positive integer.");
            }
            else if (seenRanks.Contains(rank))
            {
                validationErrors.Add($"Row {rowNum}: Duplicate Rank '{rank}'.");
            }
            else
            {
                seenRanks.Add(rank);
            }

            if (!double.TryParse(cols[scoreIdx], out var score) || score < 0)
            {
                validationErrors.Add($"Row {rowNum}: Score must be a non-negative number.");
            }

            int views = viewsIdx >= 0 && viewsIdx < cols.Length && int.TryParse(cols[viewsIdx], out var v) && v >= 0 ? v : 0;
            int likes = likesIdx >= 0 && likesIdx < cols.Length && int.TryParse(cols[likesIdx], out var l) && l >= 0 ? l : 0;
            int favorites = favoritesIdx >= 0 && favoritesIdx < cols.Length && int.TryParse(cols[favoritesIdx], out var f) && f >= 0 ? f : 0;
            int comments = commentsIdx >= 0 && commentsIdx < cols.Length && int.TryParse(cols[commentsIdx], out var c) && c >= 0 ? c : 0;
            double trendScore = trendScoreIdx >= 0 && trendScoreIdx < cols.Length && double.TryParse(cols[trendScoreIdx], out var ts) && ts >= 0 ? ts : score;

            if (validationErrors.Count == 0 || validationErrors.All(e => !e.StartsWith($"Row {rowNum}:")))
            {
                snapshotsToInsert.Add(new RankingSnapshot
                {
                    SeriesId = seriesId,
                    Rank = rank,
                    Score = score,
                    Views = views,
                    Likes = likes,
                    Favorites = favorites,
                    Comments = comments,
                    TrendScore = trendScore,
                    Period = request.Period,
                    SnapshotDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        int totalDataRows = lines.Length - 1;

        if (request.DryRun)
        {
            return new ImportRankingCsvResult(
                Success: validationErrors.Count == 0,
                BatchId: Guid.Empty,
                TotalRows: totalDataRows,
                SuccessCount: validationErrors.Count == 0 ? snapshotsToInsert.Count : 0,
                ErrorCount: validationErrors.Count,
                ValidationErrors: validationErrors,
                IsDryRun: true,
                Message: validationErrors.Count == 0 ? "CSV validation passed cleanly." : $"CSV validation failed with {validationErrors.Count} error(s)."
            );
        }

        var batch = new RankingImportBatch
        {
            UploaderId = request.UploaderId,
            UploadedAt = DateTime.UtcNow,
            Filename = request.Filename,
            FileChecksum = checksum,
            Period = request.Period,
            PeriodIdentifier = request.PeriodIdentifier,
            TotalRows = totalDataRows,
            SuccessCount = validationErrors.Count == 0 ? snapshotsToInsert.Count : 0,
            ErrorCount = validationErrors.Count,
            Status = validationErrors.Count == 0 ? "Completed" : "Failed",
            ErrorSummary = validationErrors.Count > 0 ? string.Join("; ", validationErrors.Take(10)) : null
        };

        if (validationErrors.Count > 0)
        {
            await _rankingRepo.RecordFailedBatchAsync(batch, cancellationToken);

            return new ImportRankingCsvResult(
                Success: false,
                BatchId: batch.Id,
                TotalRows: totalDataRows,
                SuccessCount: 0,
                ErrorCount: validationErrors.Count,
                ValidationErrors: validationErrors,
                IsDryRun: false,
                Message: $"CSV import failed with {validationErrors.Count} error(s). No changes committed."
            );
        }

        await _rankingRepo.ImportBatchAsync(batch, snapshotsToInsert, cancellationToken);

        return new ImportRankingCsvResult(
            Success: true,
            BatchId: batch.Id,
            TotalRows: totalDataRows,
            SuccessCount: snapshotsToInsert.Count,
            ErrorCount: 0,
            ValidationErrors: new List<string>(),
            IsDryRun: false,
            Message: $"Successfully imported {snapshotsToInsert.Count} ranking records."
        );
    }
}
