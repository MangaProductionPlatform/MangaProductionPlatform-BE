namespace MangaERP.Ranking.Domain.Entities;

public enum RankingPeriod
{
    Daily,
    Weekly,
    Monthly,
    AllTime
}

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
    public int Rank { get; set; }
    public double Score { get; set; }
    public int Views { get; set; }
    public int Likes { get; set; }
    public int Favorites { get; set; }
    public int Comments { get; set; }
    public double TrendScore { get; set; }
    public RankingPeriod Period { get; set; }
    public DateTime SnapshotDate { get; set; }
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
