namespace MangaERP.Ranking.Domain.Entities;

/// <summary>
/// [NEW] Raw reader vote data imported by Editorial Board per period.
/// One record per series per vote period.
/// </summary>
public class VoteData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string VotePeriod { get; set; } = string.Empty;  // e.g. '2025-W23', '2025-M06'
    public int VoteCount { get; set; } = 0;
    public Guid ImportedBy { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// [NEW] Aggregated ranking snapshot per period. Used for ranking board display.
/// </summary>
public class RankingSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string VotePeriod { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int TotalVotes { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
