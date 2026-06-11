namespace MangaERP.Ranking.Domain.Entities;

public class VoteData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string VotePeriod { get; set; } = string.Empty;
    public int VoteCount { get; set; } = 0;
    public Guid ImportedBy { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

public class RankingSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string VotePeriod { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int TotalVotes { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SystemAuditLog
{
    public long Id { get; set; }
    public Guid? ActorId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
