using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using TaskEntity = MangaERP.Task.Domain.Entities;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.QA.Domain.Entities;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using MangaERP.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Persistence;

/// <summary>
/// Single unified DbContext for the entire MangaERP monolith.
/// All 8 modules share one database and one migration history.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── System / Configurations ──────────────────────────────
    public DbSet<SystemConfig>     SystemConfigs     => Set<SystemConfig>();

    // ── Identity ─────────────────────────────────────────────
    public DbSet<User>             Users             => Set<User>();
    public DbSet<RefreshToken>     RefreshTokens     => Set<RefreshToken>();
    public DbSet<InvitationToken>  InvitationTokens  => Set<InvitationToken>();

    // ── RBAC ──────────────────────────────────────────────────
    public DbSet<Role_Entity>      Roles             => Set<Role_Entity>();
    public DbSet<UserRole_Entity>  UserRoles         => Set<UserRole_Entity>();

    // ── Submission ────────────────────────────────────────────
    public DbSet<SeriesSubmission> SeriesSubmissions     => Set<SeriesSubmission>();
    public DbSet<SubmissionFeedbackPin> SubmissionFeedbackPins => Set<SubmissionFeedbackPin>();
    public DbSet<SubmissionVote>   SubmissionVotes       => Set<SubmissionVote>();
    public DbSet<EditorialReviewAssignment> EditorialReviewAssignments => Set<EditorialReviewAssignment>();

    // ── Series ────────────────────────────────────────────────
    public DbSet<MangaSeries>      MangaSeries       => Set<MangaSeries>();

    // ── Chapter ───────────────────────────────────────────────
    public DbSet<ChapterEntity>    Chapters          => Set<ChapterEntity>();
    public DbSet<PageTask>         PageTasks         => Set<PageTask>();
    public DbSet<PreviewPage>      PreviewPages      => Set<PreviewPage>();

    // ── Task ──────────────────────────────────────────────────
    public DbSet<ArtworkLayer>        ArtworkLayers        => Set<ArtworkLayer>();
    public DbSet<AssistantInvitation> AssistantInvitations => Set<AssistantInvitation>();
    public DbSet<ChapterTeam>         ChapterTeams         => Set<ChapterTeam>();
    public DbSet<TaskComment>         TaskComments         => Set<TaskComment>();
    public DbSet<DeadlineExtensionRequest> DeadlineExtensionRequests => Set<DeadlineExtensionRequest>();

    // ── Studio ────────────────────────────────────────────────
    public DbSet<StudioInvitation> StudioInvitations => Set<StudioInvitation>();

    // ── QA ────────────────────────────────────────────────────
    public DbSet<BugPin>     BugPins     => Set<BugPin>();
    public DbSet<QASession>  QASessions  => Set<QASession>();

    // ── Publishing ────────────────────────────────────────────
    public DbSet<PublicationRecord> PublicationRecords => Set<PublicationRecord>();
    public DbSet<Notification>      Notifications      => Set<Notification>();

    // ── Ranking / System ─────────────────────────────────────
    public DbSet<VoteData>        VoteData         => Set<VoteData>();
    public DbSet<RankingSnapshot> RankingSnapshots => Set<RankingSnapshot>();
    public DbSet<SystemAuditLog>  SystemAuditLogs  => Set<SystemAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override System.Threading.Tasks.Task<int> SaveChangesAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Deleted) continue;
            var entity = entry.Entity;
            var isDeletedProp = entity.GetType().GetProperty("IsDeleted");
            var deletedAtProp = entity.GetType().GetProperty("DeletedAt");
            if (isDeletedProp == null) continue;
            entry.State = EntityState.Modified;
            isDeletedProp.SetValue(entity, true);
            deletedAtProp?.SetValue(entity, DateTime.UtcNow);
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
