using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using PageTaskType = MangaERP.Chapter.Domain.Entities.PageTaskType;
using PageTaskStatus = MangaERP.Chapter.Domain.Entities.PageTaskStatus;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class EndToEndAssistantWorkflowTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async STTask CompleteAssistantLifecycle_FromInvitationToTaskCompletion_Succeeds()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        Guid mangakaId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();

        // 1. Create Mangaka-wide Collaboration
        Guid invitationId = Guid.NewGuid();
        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, invitationId, DateTime.UtcNow);
        await db.MangakaAssistantCollaborations.AddAsync(collab);

        // 2. Grant Series Access
        var grant = SeriesAccessGrant.Create(collab.Id, seriesId, mangakaId);
        await db.SeriesAccessGrants.AddAsync(grant);

        // 3. Create Chapter & Task
        var chapter = ChapterEntity.Create(seriesId, "Chapter 10", 10, 20);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        await db.Chapters.AddAsync(chapter);

        var task = new PageTaskEntity { ChapterId = chapterId, PageNumber = 1, TaskType = PageTaskType.Inking };
        typeof(PageTaskEntity).GetProperty("Id")!.SetValue(task, taskId);
        await db.PageTasks.AddAsync(task);

        // 4. Direct Task Assignment to Assistant (Effective immediately)
        DateTime assignedAt = DateTime.UtcNow;
        TimeSpan duration = TimeSpan.FromHours(24);
        var attempt = TaskAssignmentAttempt.CreateAccepted(taskId, assistantId, collab.Id, 1, mangakaId, assignedAt);
        task.AssignDirect(assistantId, "Inking page task", assignedAt.Add(duration), assignedAt);

        await db.TaskAssignmentAttempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        // Verify task state immediately on direct assignment
        Assert.Equal(assistantId, task.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Incomplete, task.TaskStatus);
        Assert.Equal(assignedAt, task.WorkStartedAt);
        Assert.Equal(assignedAt.Add(duration), task.Deadline);

        // 5. Progress Updates (Non-decreasing 0-100)
        task.SubmitProgress(25);
        Assert.Equal(25, task.ProgressPercent);

        task.SubmitProgress(75);
        Assert.Equal(75, task.ProgressPercent);

        // Submitting lower progress throws InvalidOperationException
        Action invalidProgressAction = () => task.SubmitProgress(50);
        Assert.Throws<InvalidOperationException>(invalidProgressAction);

        // 6. Complete Task
        DateTime completedAt = DateTime.UtcNow;
        task.CompleteTask(completedAt);

        Assert.Equal(100, task.ProgressPercent);
        Assert.Equal(PageTaskStatus.Reviewing, task.TaskStatus);

        await db.SaveChangesAsync();
    }
}
