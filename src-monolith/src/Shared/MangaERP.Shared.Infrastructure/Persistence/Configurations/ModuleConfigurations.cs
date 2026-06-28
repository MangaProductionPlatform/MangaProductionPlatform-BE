using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.QA.Domain.Entities;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MangaERP.Shared.Infrastructure.Persistence.Configurations;

public class SeriesSubmissionConfiguration : IEntityTypeConfiguration<SeriesSubmission>
{
    public void Configure(EntityTypeBuilder<SeriesSubmission> b)
    {
        b.ToTable("SeriesSubmissions"); b.HasKey(e => e.Id);
        b.Property(e => e.Title).IsRequired().HasMaxLength(256);
        b.Property(e => e.ManuscriptUrl).IsRequired(false).HasMaxLength(2048);
        b.Property(e => e.Status).HasConversion(v => v.ToString(), v => Enum.Parse<SubmissionStatus>(v)).HasMaxLength(50);
        b.Property(e => e.CurrentRound).HasDefaultValue(1);
        b.HasQueryFilter(e => !e.IsDeleted);

        // 1:N relationship with FeedbackPins
        b.HasMany<SubmissionFeedbackPin>()
         .WithOne()
         .HasForeignKey(p => p.SubmissionId)
         .OnDelete(DeleteBehavior.Cascade);

        // 1:N relationship with SubmissionVotes
        b.HasMany<SubmissionVote>()
         .WithOne()
         .HasForeignKey(p => p.SubmissionId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubmissionVoteConfiguration : IEntityTypeConfiguration<SubmissionVote>
{
    public void Configure(EntityTypeBuilder<SubmissionVote> b)
    {
        b.ToTable("SubmissionVotes"); b.HasKey(e => e.Id);
        b.Property(e => e.VoteType)
            .HasConversion(v => v.ToString(), v => Enum.Parse<VoteType>(v))
            .HasMaxLength(50)
            .IsRequired();
        b.Property(e => e.Comment).HasMaxLength(2000);
        b.Property(e => e.RoundNumber).HasDefaultValue(1);
        // Composite index: ensures unique vote per editor per submission per round
        b.HasIndex(e => new { e.SubmissionId, e.EditorId, e.RoundNumber }).IsUnique();
        b.HasIndex(e => new { e.SubmissionId, e.RoundNumber });
    }
}

public class SubmissionFeedbackPinConfiguration : IEntityTypeConfiguration<SubmissionFeedbackPin>
{
    public void Configure(EntityTypeBuilder<SubmissionFeedbackPin> b)
    {
        b.ToTable("SubmissionFeedbackPins"); b.HasKey(e => e.Id);
        b.Property(e => e.PageIdentifier).IsRequired().HasMaxLength(2048);
        b.Property(e => e.CoordinateX).HasColumnType("decimal(5,2)");
        b.Property(e => e.CoordinateY).HasColumnType("decimal(5,2)");
        b.Property(e => e.Comment).IsRequired().HasMaxLength(2000);
        b.Property(e => e.Category).HasConversion(
            v => v.ToString(), v => Enum.Parse<FeedbackPinCategory>(v)).HasMaxLength(50);
        b.HasIndex(e => new { e.SubmissionId, e.IsArchived });
    }
}

public class MangaSeriesConfiguration : IEntityTypeConfiguration<MangaSeries>
{
    public void Configure(EntityTypeBuilder<MangaSeries> b)
    {
        b.ToTable("MangaSeries"); b.HasKey(e => e.Id);
        b.Property(e => e.Title).IsRequired().HasMaxLength(256);
        b.Property(e => e.Status).HasConversion(v => v.ToString(), v => Enum.Parse<SeriesStatus>(v)).HasMaxLength(50);
        b.HasIndex(e => e.SubmissionId).IsUnique();
        b.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ChapterConfiguration : IEntityTypeConfiguration<ChapterEntity>
{
    public void Configure(EntityTypeBuilder<ChapterEntity> b)
    {
        b.ToTable("Chapters"); b.HasKey(e => e.Id);
        b.Property(e => e.Title).IsRequired().HasMaxLength(256);
        b.Property(e => e.CoverImageUrl).HasMaxLength(2048);
        b.Property(e => e.ChapterNumber).HasColumnType("decimal(5,2)");
        b.Property(e => e.Status).HasConversion(v => v.ToString(), v => Enum.Parse<ChapterStatus>(v)).HasMaxLength(50);
        b.Property(e => e.IssueType).HasMaxLength(50);
        b.HasMany(c => c.PageTasks).WithOne(pt => pt.Chapter)
            .HasForeignKey(pt => pt.ChapterId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PageTaskConfiguration : IEntityTypeConfiguration<PageTask>
{
    public void Configure(EntityTypeBuilder<PageTask> b)
    {
        b.ToTable("PageTasks"); b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.ChapterId, e.PageNumber }).IsUnique();
        b.Property(e => e.Description).HasMaxLength(2000);
        b.Property(e => e.TaskStatus).HasConversion(v => v.ToString(),
            v => Enum.Parse<PageTaskStatus>(v)).HasMaxLength(50);
        b.Property(e => e.TaskType).HasConversion(v => v.ToString(),
            v => Enum.Parse<PageTaskType>(v)).HasMaxLength(50).HasDefaultValue(PageTaskType.General);
        b.Property(e => e.RegionMask).HasColumnType("text").IsRequired(false);
        b.HasOne(pt => pt.PreviewPage).WithOne(pp => pp.PageTask)
            .HasForeignKey<PreviewPage>(pp => pp.PageTaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PreviewPageConfiguration : IEntityTypeConfiguration<PreviewPage>
{
    public void Configure(EntityTypeBuilder<PreviewPage> b)
    {
        b.ToTable("PreviewPages"); b.HasKey(e => e.Id);
        b.HasIndex(e => e.PageTaskId).IsUnique();
        b.Property(e => e.CompositeFileUrl).IsRequired().HasMaxLength(2048);
        b.Property(e => e.ProductionFileUrl).HasMaxLength(2048);
    }
}

public class ArtworkLayerConfiguration : IEntityTypeConfiguration<ArtworkLayer>
{
    public void Configure(EntityTypeBuilder<ArtworkLayer> b)
    {
        b.ToTable("ArtworkLayers"); b.HasKey(e => e.Id);
        b.Property(e => e.LayerType).IsRequired().HasMaxLength(50);
        b.Property(e => e.FileUrlOriginal).IsRequired().HasMaxLength(2048);
        b.Property(e => e.FileUrlOptimized).IsRequired().HasMaxLength(2048);
    }
}

public class AssistantInvitationConfiguration : IEntityTypeConfiguration<AssistantInvitation>
{
    public void Configure(EntityTypeBuilder<AssistantInvitation> b)
    {
        b.ToTable("AssistantInvitations"); b.HasKey(e => e.Id);
        b.HasIndex(e => e.InvitationToken).IsUnique();
        b.Property(e => e.AssignedRole).IsRequired().HasMaxLength(100);
        b.Property(e => e.Email).IsRequired().HasMaxLength(256);
    }
}

public class ChapterTeamConfiguration : IEntityTypeConfiguration<ChapterTeam>
{
    public void Configure(EntityTypeBuilder<ChapterTeam> b)
    {
        b.ToTable("ChapterTeams"); b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.ChapterId, e.UserId, e.AssignedRole }).IsUnique();
        b.Property(e => e.AssignedRole).IsRequired().HasMaxLength(100);
    }
}

public class BugPinConfiguration : IEntityTypeConfiguration<BugPin>
{
    public void Configure(EntityTypeBuilder<BugPin> b)
    {
        b.ToTable("BugPins"); b.HasKey(e => e.Id);
        b.Property(e => e.CoordinateX).HasColumnType("decimal(5,2)");
        b.Property(e => e.CoordinateY).HasColumnType("decimal(5,2)");
        b.Property(e => e.NoteMessage).IsRequired();
        b.Property(e => e.Status).IsRequired().HasMaxLength(50);
        b.Property(e => e.IssueType).HasMaxLength(50);
        b.HasIndex(e => new { e.ChapterId, e.Status });
    }
}

public class QASessionConfiguration : IEntityTypeConfiguration<QASession>
{
    public void Configure(EntityTypeBuilder<QASession> b)
    {
        b.ToTable("QASessions"); b.HasKey(e => e.Id);
        b.HasIndex(e => e.ChapterId).IsUnique();
        b.Property(e => e.Status).IsRequired().HasMaxLength(50);
    }
}

public class PublicationRecordConfiguration : IEntityTypeConfiguration<PublicationRecord>
{
    public void Configure(EntityTypeBuilder<PublicationRecord> b)
    {
        b.ToTable("PublicationRecords"); b.HasKey(e => e.Id);
        b.Property(e => e.IssueType).IsRequired().HasMaxLength(50);
        b.Property(e => e.PublicationUrl).HasMaxLength(2048);
        b.Property(e => e.CacheKey).HasMaxLength(512);
        b.HasIndex(e => e.ChapterId);
        b.HasIndex(e => e.SeriesId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications"); b.HasKey(e => e.Id);
        b.Property(e => e.Title).IsRequired().HasMaxLength(256);
        b.Property(e => e.NotifyType).IsRequired().HasMaxLength(100);
        b.Property(e => e.RelatedEntityType).HasMaxLength(50);
        b.Property(e => e.TargetUrl).HasMaxLength(2048);
        b.HasIndex(e => new { e.ReceiverId, e.IsRead });
    }
}

public class VoteDataConfiguration : IEntityTypeConfiguration<VoteData>
{
    public void Configure(EntityTypeBuilder<VoteData> b)
    {
        b.ToTable("VoteData"); b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.SeriesId, e.VotePeriod }).IsUnique();
        b.Property(e => e.VotePeriod).IsRequired().HasMaxLength(50);
    }
}

public class RankingSnapshotConfiguration : IEntityTypeConfiguration<RankingSnapshot>
{
    public void Configure(EntityTypeBuilder<RankingSnapshot> b)
    {
        b.ToTable("RankingSnapshots"); b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.SeriesId, e.VotePeriod }).IsUnique();
        b.Property(e => e.VotePeriod).IsRequired().HasMaxLength(50);
        b.HasIndex(e => new { e.VotePeriod, e.Rank });
    }
}

public class SystemAuditLogConfiguration : IEntityTypeConfiguration<SystemAuditLog>
{
    public void Configure(EntityTypeBuilder<SystemAuditLog> b)
    {
        b.ToTable("SystemAuditLogs"); b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd();
        b.Property(e => e.ActionName).IsRequired().HasMaxLength(256);
        b.Property(e => e.EntityType).HasMaxLength(50);
        b.Property(e => e.IpAddress).HasMaxLength(50);
        b.HasIndex(e => new { e.Timestamp, e.ActorId });
    }
}

public class StudioInvitationConfiguration : IEntityTypeConfiguration<StudioInvitation>
{
    public void Configure(EntityTypeBuilder<StudioInvitation> b)
    {
        b.ToTable("StudioInvitations"); b.HasKey(e => e.Id);
        b.Property(e => e.AssistantEmail).IsRequired().HasMaxLength(256);
        b.Property(e => e.Status).HasConversion(v => v.ToString(),
            v => Enum.Parse<StudioInvitationStatus>(v)).HasMaxLength(50);
        b.Property(e => e.Message).HasMaxLength(1000);
        b.Property(e => e.ActivationToken).HasMaxLength(2048);
        b.HasIndex(e => new { e.SeriesId, e.AssistantEmail });
        b.HasIndex(e => new { e.AssistantUserId, e.Status });
    }
}
