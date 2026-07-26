using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using PageTaskType = MangaERP.Chapter.Domain.Entities.PageTaskType;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class PostgreSqlPhase567Tests
{
    private string GetPostgreSqlConnectionString()
    {
        return Environment.GetEnvironmentVariable("PHASE1_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=MangaProductionDB;Username=postgres;Password=MangaERP_Pass1234!";
    }

    private AppDbContext CreatePostgreSqlDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetPostgreSqlConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async STTask PostgreSql_ProgressUpdatesAndAuditTrail_PersistedSuccessfully()
    {
        using var db = CreatePostgreSqlDbContext();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();
        Guid attemptId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();

        // Create parent Chapter & PageTask to satisfy foreign key constraints
        var chapter = ChapterEntity.Create(seriesId, "PG Test Ch", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 1, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        var update = new TaskProgressUpdate(taskId, attemptId, assistantId, 45, "Inked background elements.", assistantId);
        var audit = new AuditEvent("ProgressSubmitted", assistantId, "PageTask", taskId, taskId: taskId, metadataJson: "{\"progress\":45}");

        await db.TaskProgressUpdates.AddAsync(update);
        await db.AuditEvents.AddAsync(audit);
        await db.SaveChangesAsync();

        var savedUpdate = await db.TaskProgressUpdates.FirstOrDefaultAsync(u => u.Id == update.Id);
        var savedAudit = await db.AuditEvents.FirstOrDefaultAsync(a => a.Id == audit.Id);

        Assert.NotNull(savedUpdate);
        Assert.Equal(45, savedUpdate.ProgressPercent);
        Assert.Equal("Inked background elements.", savedUpdate.Note);

        Assert.NotNull(savedAudit);
        Assert.Equal("ProgressSubmitted", savedAudit.Action);

        // Cleanup
        db.TaskProgressUpdates.Remove(savedUpdate);
        db.AuditEvents.Remove(savedAudit);
        db.PageTasks.Remove(task);
        db.Chapters.Remove(chapter);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async STTask PostgreSql_TaskCheckpoints_ComputeStatusCorrectly()
    {
        using var db = CreatePostgreSqlDbContext();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();

        // Create parent Chapter & PageTask to satisfy foreign key constraints
        var chapter = ChapterEntity.Create(seriesId, "PG Test Ch 2", 2, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 2, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        var checkpoint = new TaskCheckpoint(taskId, "Midpoint Review", 50, 120, true);
        await db.TaskCheckpoints.AddAsync(checkpoint);
        await db.SaveChangesAsync();

        var saved = await db.TaskCheckpoints.FirstOrDefaultAsync(c => c.Id == checkpoint.Id);
        Assert.NotNull(saved);

        DateTime acceptedAt = DateTime.UtcNow.AddMinutes(-60);
        var status = saved.ComputeStatus(acceptedAt, 60, DateTime.UtcNow);
        Assert.Equal(CheckpointStatus.Met, status);

        // Cleanup
        db.TaskCheckpoints.Remove(saved);
        db.PageTasks.Remove(task);
        db.Chapters.Remove(chapter);
        await db.SaveChangesAsync();
    }
}
