using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using PageTaskType = MangaERP.Chapter.Domain.Entities.PageTaskType;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class LegacyRouteAuthorizationTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async STTask CanAccessTaskAsync_LegacyInvitationWithoutActiveGrant_ReturnsFalse()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        Guid mangakaId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();

        // Setup Chapter & PageTask
        var chapter = ChapterEntity.Create(seriesId, "Ch 1", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 1, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        // Setup Legacy StudioInvitation (Accepted), but NO active MangakaAssistantCollaboration or SeriesAccessGrant
        var invitation = new StudioInvitation
        {
            InviterMangakaId = mangakaId,
            SeriesId = seriesId,
            AssistantEmail = "asst@test.com",
            NormalizedAssistantEmail = "ASST@TEST.COM",
            AssistantUserId = assistantId,
            Status = StudioInvitationStatus.Accepted,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await db.StudioInvitations.AddAsync(invitation);

        await db.SaveChangesAsync();

        // Act
        bool canAccess = await authService.CanAccessTaskAsync(assistantId, taskId);

        // Assert
        Assert.False(canAccess, "Access must be denied when only legacy invitation exists without active collaboration & grant.");
    }

    [Fact]
    public async STTask CanAccessTaskAsync_ActiveCollaborationWithGrant_ReturnsTrue()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        Guid mangakaId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();

        var chapter = ChapterEntity.Create(seriesId, "Ch 1", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 1, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, invitationId, DateTime.UtcNow);
        await db.MangakaAssistantCollaborations.AddAsync(collab);

        var grant = SeriesAccessGrant.Create(collab.Id, seriesId, mangakaId);
        await db.SeriesAccessGrants.AddAsync(grant);

        await db.SaveChangesAsync();

        // Act
        bool canAccess = await authService.CanAccessTaskAsync(assistantId, taskId);

        // Assert
        Assert.True(canAccess, "Access must be granted when active collaboration and series grant exist.");
    }

    [Fact]
    public async STTask CanAccessTaskAsync_SuspendedAllAccess_ReturnsFalse()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        Guid mangakaId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();

        var chapter = ChapterEntity.Create(seriesId, "Ch 1", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 1, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, invitationId, DateTime.UtcNow);
        collab.Suspend(CollaborationSuspensionMode.SuspendAllAccess, "Suspended for review", DateTime.UtcNow);
        await db.MangakaAssistantCollaborations.AddAsync(collab);

        var grant = SeriesAccessGrant.Create(collab.Id, seriesId, mangakaId);
        await db.SeriesAccessGrants.AddAsync(grant);

        await db.SaveChangesAsync();

        // Act
        bool canAccess = await authService.CanAccessTaskAsync(assistantId, taskId);

        // Assert
        Assert.False(canAccess, "Access must be denied under SuspendAllAccess mode.");
    }
}
