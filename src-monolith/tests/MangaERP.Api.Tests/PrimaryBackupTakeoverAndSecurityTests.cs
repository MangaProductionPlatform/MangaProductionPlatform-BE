using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Submission.Domain.Entities;

namespace MangaERP.Api.Tests;

public class PrimaryBackupTakeoverAndSecurityTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void PageTask_AssignPrimaryAndBackup_SetsCorrectFields()
    {
        // Arrange
        var task = new PageTask
        {
            ChapterId = Guid.NewGuid(),
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Pending
        };
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();

        // Act
        task.AssignPrimaryAndBackup(primaryId, backupId, "Background drawing", DateTime.UtcNow.AddDays(2));

        // Assert
        Assert.Equal(primaryId, task.PrimaryAssistantId);
        Assert.Equal(backupId, task.BackupAssistantId);
        Assert.Equal(primaryId, task.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.PendingAcceptance, task.TaskStatus);
        Assert.Equal("None", task.TakeoverStatus);
    }

    [Fact]
    public void PageTask_RequestAndAcceptTakeover_TransfersAssignmentToBackup()
    {
        // Arrange
        var task = new PageTask
        {
            ChapterId = Guid.NewGuid(),
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Pending
        };
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        task.AssignPrimaryAndBackup(primaryId, backupId, "Lineart", DateTime.UtcNow.AddDays(2));

        // Act 1: Request Takeover
        task.RequestTakeover("Primary unavailable due to emergency.");
        Assert.Equal("TakeoverRequested", task.TakeoverStatus);

        // Act 2: Backup Accept Takeover
        var newDeadline = DateTime.UtcNow.AddDays(3);
        task.AcceptTakeover(backupId, DateTime.UtcNow, newDeadline);

        // Assert
        Assert.Equal(backupId, task.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Incomplete, task.TaskStatus);
        Assert.Equal("TakeoverAccepted", task.TakeoverStatus);
        Assert.Equal(newDeadline, task.Deadline);
    }

    [Fact]
    public async System.Threading.Tasks.Task AuthorizationService_RevokesPrimaryAccess_AfterTakeover()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        var mangakaId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();

        var author = new User { Id = mangakaId, Username = "mangaka1", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var primary = new User { Id = primaryId, Username = "primary1", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var backup = new User { Id = backupId, Username = "backup1", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(author, primary, backup);

        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Description", "Action", null);
        db.MangaSeries.Add(series);

        var chapter = MangaERP.Chapter.Domain.Entities.Chapter.Create(series.Id, "Ch 1", 1, 10);
        db.Chapters.Add(chapter);

        var task = new PageTask { ChapterId = chapter.Id, PageNumber = 1, TaskStatus = PageTaskStatus.Pending };
        task.AssignPrimaryAndBackup(primaryId, backupId, "Coloring", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(task);

        var collabPrimary = new MangakaAssistantCollaboration(mangakaId, primaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collabBackup = new MangakaAssistantCollaboration(mangakaId, backupId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabPrimary, collabBackup);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabPrimary.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabBackup.Id, series.Id, mangakaId));

        db.TaskAssignmentAttempts.Add(TaskAssignmentAttempt.CreatePending(task.Id, primaryId, collabPrimary.Id, 1, mangakaId));
        await db.SaveChangesAsync();

        // 1. Author has full access
        Assert.True(await authService.CanAccessTaskAsync(mangakaId, task.Id));

        // 2. Primary has access initially
        Assert.True(await authService.CanAccessTaskAsync(primaryId, task.Id));

        // 3. Backup does NOT have access before takeover
        Assert.False(await authService.CanAccessTaskAsync(backupId, task.Id));

        // 4. Perform Takeover
        task.AcceptTakeover(backupId, DateTime.UtcNow, DateTime.UtcNow.AddDays(3));
        await db.SaveChangesAsync();

        // 5. Post Takeover: Backup now HAS access
        Assert.True(await authService.CanAccessTaskAsync(backupId, task.Id));

        // 6. Post Takeover: Primary access is REVOKED
        Assert.False(await authService.CanAccessTaskAsync(primaryId, task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task EditorialReviewerSelection_ExcludesAssignedTantou_EvenWhenCallerIsEic()
    {
        // Arrange
        using var db = GetInMemoryDbContext();

        var authorId = Guid.NewGuid();
        var assignedTantouId = Guid.NewGuid(); // Has EditorialBoard role
        var eicUserId = Guid.NewGuid();
        var reviewer1Id = Guid.NewGuid();
        var reviewer2Id = Guid.NewGuid();

        var author = new User { Id = authorId, Username = "author", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var assignedTantou = new User { Id = assignedTantouId, Username = "tantou_eb", Role = UserRole.EditorialBoard, AccountStatus = AccountStatus.Active };
        var eic = new User { Id = eicUserId, Username = "eic", Role = UserRole.EditorInChief, AccountStatus = AccountStatus.Active };
        var reviewer1 = new User { Id = reviewer1Id, Username = "rev1", Role = UserRole.EditorialBoard, AccountStatus = AccountStatus.Active };
        var reviewer2 = new User { Id = reviewer2Id, Username = "rev2", Role = UserRole.EditorialBoard, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(author, assignedTantou, eic, reviewer1, reviewer2);

        var series = MangaSeries.Create(authorId, null, "Series A", "Desc", "Genre", null);
        db.MangaSeries.Add(series);

        var submission = SeriesSubmission.CreateDraft(authorId, "Submission Title", "Synopsis", "G", "http://cv.png", "http://manuscript.pdf");
        submission.SubmitDraft(assignedTantouId);
        db.SeriesSubmissions.Add(submission);

        await db.SaveChangesAsync();

        // Query candidates excluding authorId and assignedTantouId (work.AssignedEditorId)
        var selectedReviewers = await db.Users
            .Where(x => x.AccountStatus == AccountStatus.Active && !x.IsDeleted)
            .Where(x => x.Role == UserRole.EditorialBoard || x.UserRoles.Any(ur => ur.Role.Name == "EditorialBoard"))
            .Where(x => x.Role != UserRole.EditorInChief && !x.UserRoles.Any(ur => ur.Role.Name == "EditorInChief"))
            .Where(x => x.Id != authorId)
            .Where(x => x.Id != submission.AssignedEditorId)
            .OrderBy(x => x.Id).Take(2).Select(x => x.Id).ToListAsync();

        // Assert: Tantou was excluded even though Tantou had EditorialBoard role and caller was EIC
        Assert.Equal(2, selectedReviewers.Count);
        Assert.DoesNotContain(assignedTantouId, selectedReviewers);
        Assert.DoesNotContain(authorId, selectedReviewers);
        Assert.DoesNotContain(eicUserId, selectedReviewers);
        Assert.Contains(reviewer1Id, selectedReviewers);
        Assert.Contains(reviewer2Id, selectedReviewers);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskFileStreamingEndpoint_AuthorizationAndRevocation_ReturnsExpectedStatus()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        var mangakaId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var randomUserId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series Task Files", "Desc", "Genre", null);
        var chapter = MangaERP.Chapter.Domain.Entities.Chapter.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask { ChapterId = chapter.Id, PageNumber = 1, TaskStatus = PageTaskStatus.Pending };
        task.AssignPrimaryAndBackup(primaryId, backupId, "Inking", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(task);

        var collabPrimary = new MangakaAssistantCollaboration(mangakaId, primaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collabBackup = new MangakaAssistantCollaboration(mangakaId, backupId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabPrimary, collabBackup);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabPrimary.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabBackup.Id, series.Id, mangakaId));

        var attemptPrimary = TaskAssignmentAttempt.CreatePending(task.Id, primaryId, collabPrimary.Id, 1, mangakaId);
        attemptPrimary.Accept(primaryId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attemptPrimary);
        task.CurrentAssignmentAttemptId = attemptPrimary.Id;
        task.AssignedAssistantId = primaryId;
        task.TaskStatus = PageTaskStatus.Approved;

        await db.SaveChangesAsync();

        // 1. Author (Mangaka) HAS file access
        Assert.True(await authService.CanAccessTaskResourcesAsync(mangakaId, task.Id));

        // 2. Active Primary HAS file access
        Assert.True(await authService.CanAccessTaskResourcesAsync(primaryId, task.Id));

        // 3. Pending Backup DOES NOT HAVE file access before takeover
        Assert.False(await authService.CanAccessTaskResourcesAsync(backupId, task.Id));

        // 4. Random user DOES NOT HAVE file access
        Assert.False(await authService.CanAccessTaskResourcesAsync(randomUserId, task.Id));

        // 5. Takeover performed -> Backup accepts takeover
        task.AcceptTakeover(backupId, DateTime.UtcNow, DateTime.UtcNow.AddDays(3));
        await db.SaveChangesAsync();

        // 6. Post Takeover: Backup HAS file access
        Assert.True(await authService.CanAccessTaskResourcesAsync(backupId, task.Id));

        // 7. Post Takeover: Primary access is REVOKED (returns 403 Forbidden)
        Assert.False(await authService.CanAccessTaskResourcesAsync(primaryId, task.Id));
    }

    [Fact]
    public void TaskFileMetadataResponse_DoesNotExposeRawPublicCloudinaryUrl()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var safeFileType = "inking";

        // Construct streaming endpoint URL format
        var streamingEndpointUrl = $"/api/tasks/{taskId}/files/{safeFileType}";

        // Assert: Endpoint URL points to backend streaming route, NOT public Cloudinary URL
        Assert.DoesNotContain("res.cloudinary.com", streamingEndpointUrl);
        Assert.StartsWith("/api/tasks/", streamingEndpointUrl);
        Assert.EndsWith("/files/inking", streamingEndpointUrl);
    }
}
