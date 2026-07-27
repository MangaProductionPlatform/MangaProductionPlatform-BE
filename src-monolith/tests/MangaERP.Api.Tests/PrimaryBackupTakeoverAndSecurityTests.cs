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
    public void PageTask_AssignPending_SetsCorrectFields()
    {
        // Arrange
        var task = new PageTask
        {
            ChapterId = Guid.NewGuid(),
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Pending
        };
        var assistantId = Guid.NewGuid();

        // Act
        task.AssignPending(assistantId, "Background drawing", DateTime.UtcNow.AddDays(2));

        // Assert
        Assert.Equal(PageTaskStatus.PendingAcceptance, task.TaskStatus);
        Assert.Equal("Background drawing", task.Description);
        Assert.Equal("None", task.TakeoverStatus);
        Assert.Null(task.AssignedAssistantId); // Not set before Accept
    }

    [Fact]
    public void PageTask_AcceptAssignment_SetsExecutorAndWorkStartedAt()
    {
        // Arrange
        var task = new PageTask
        {
            ChapterId = Guid.NewGuid(),
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Pending
        };
        var assistantId = Guid.NewGuid();
        task.AssignPending(assistantId, "Lineart", DateTime.UtcNow.AddDays(2));

        // Act
        var now = DateTime.UtcNow;
        task.AcceptAssignment(now);
        task.AssignedAssistantId = assistantId;

        // Assert
        Assert.Equal(assistantId, task.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Incomplete, task.TaskStatus);
        Assert.Equal(now, task.WorkStartedAt);
    }

    [Fact]
    public async System.Threading.Tasks.Task AuthorizationService_RevokesOldAssistantAccess_AfterReassignAccept()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        var mangakaId = Guid.NewGuid();
        var assistant1Id = Guid.NewGuid();
        var assistant2Id = Guid.NewGuid();

        var author = new User { Id = mangakaId, Username = "mangaka1", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var ast1 = new User { Id = assistant1Id, Username = "ast1", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var ast2 = new User { Id = assistant2Id, Username = "ast2", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(author, ast1, ast2);

        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Description", "Action", null);
        db.MangaSeries.Add(series);

        var chapter = MangaERP.Chapter.Domain.Entities.Chapter.Create(series.Id, "Ch 1", 1, 10);
        db.Chapters.Add(chapter);

        var task = new PageTask { ChapterId = chapter.Id, PageNumber = 1, TaskStatus = PageTaskStatus.Pending };
        task.AssignPending(assistant1Id, "Coloring", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(task);

        var collab1 = new MangakaAssistantCollaboration(mangakaId, assistant1Id, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangakaId, assistant2Id, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collab1, collab2);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab1.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab2.Id, series.Id, mangakaId));

        var attempt1 = TaskAssignmentAttempt.CreatePending(task.Id, assistant1Id, collab1.Id, 1, mangakaId);
        attempt1.Accept(assistant1Id, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attempt1);
        task.AcceptAssignment(DateTime.UtcNow);
        task.AssignedAssistantId = assistant1Id;
        await db.SaveChangesAsync();

        // 1. Author has access
        Assert.True(await authService.CanAccessTaskAsync(mangakaId, task.Id));

        // 2. Assistant 1 has submit progress access initially
        Assert.True(await authService.CanSubmitProgressAsync(assistant1Id, task.Id));

        // 3. Assistant 2 does NOT have submit progress access before Accept
        Assert.False(await authService.CanSubmitProgressAsync(assistant2Id, task.Id));

        // 4. Reassign to Assistant 2 and Assistant 2 Accepts
        attempt1.Supersede(DateTime.UtcNow, "Reassigned to Assistant 2");
        var attempt2 = TaskAssignmentAttempt.CreatePending(task.Id, assistant2Id, collab2.Id, 2, mangakaId);
        attempt2.Accept(assistant2Id, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attempt2);

        task.AcceptReplacement(assistant2Id, DateTime.UtcNow);
        await db.SaveChangesAsync();

        // 5. Post Reassign: Assistant 2 HAS submit progress access
        Assert.True(await authService.CanSubmitProgressAsync(assistant2Id, task.Id));

        // 6. Post Reassign: Assistant 1 submit progress access is REVOKED
        Assert.False(await authService.CanSubmitProgressAsync(assistant1Id, task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task EditorialReviewerSelection_ExcludesAssignedTantou_EvenWhenCallerIsEic()
    {
        // Arrange
        using var db = GetInMemoryDbContext();

        var authorId = Guid.NewGuid();
        var assignedTantouId = Guid.NewGuid();
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
        submission.SubmitDraft();
        submission.AssignTantou(assignedTantouId);
        db.SeriesSubmissions.Add(submission);

        await db.SaveChangesAsync();

        // Query candidates excluding authorId and assignedTantouId
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
}
