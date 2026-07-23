using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class TaskAssignmentWorkflowTests
{
    private readonly Mock<IPageTaskRepository> _taskRepo = new();
    private readonly Mock<IChapterRepository> _chapterRepo = new();
    private readonly Mock<ISeriesRepository> _seriesRepo = new();
    private readonly Mock<IStudioInvitationRepository> _collabRepo = new();
    private readonly Mock<ISeriesAccessGrantRepository> _grantRepo = new();
    private readonly Mock<ITaskAssignmentAttemptRepository> _attemptRepo = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    private readonly AssignTaskToAssistantHandler _assignHandler;
    private readonly RespondTaskAssignmentHandler _respondHandler;

    public TaskAssignmentWorkflowTests()
    {
        _assignHandler = new AssignTaskToAssistantHandler(
            _taskRepo.Object, _chapterRepo.Object, _seriesRepo.Object,
            _collabRepo.Object, _grantRepo.Object, _attemptRepo.Object,
            _notifications.Object, _config);

        _respondHandler = new RespondTaskAssignmentHandler(
            _attemptRepo.Object, _taskRepo.Object, _collabRepo.Object,
            _grantRepo.Object, _chapterRepo.Object, _notifications.Object);
    }

    [Fact]
    public async STTask AssignTask_CreatesPendingAttempt_AndDoesNotStartWorkTime()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series Title", "Desc", null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 10);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "http://image.url");

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var grant = SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId);

        _taskRepo.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pageTask);
        _chapterRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        _seriesRepo.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(series);
        _collabRepo.Setup(r => r.GetNonEndedCollaborationByAssistantAsync(assistantId, It.IsAny<CancellationToken>())).ReturnsAsync(collab);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collab.Id, series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        _attemptRepo.Setup(r => r.GetPendingByTaskIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync((TaskAssignmentAttempt?)null);
        _attemptRepo.Setup(r => r.GetAcceptedByTaskIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync((TaskAssignmentAttempt?)null);
        _attemptRepo.Setup(r => r.GetActiveWorkloadCountAsync(assistantId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _assignHandler.Handle(
            new AssignTaskToAssistantCommand(pageTask.Id, assistantId, mangakaId, "Background work", null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PendingAcceptance", result.Status);
        Assert.Equal(PageTaskStatus.PendingAcceptance, pageTask.TaskStatus);
        Assert.Null(pageTask.WorkStartedAt);
    }

    [Fact]
    public async STTask AssistantAccept_SetsWorkStartedAt_EqualToAcceptedAt()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series Title", "Desc", null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 10);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "http://image.url");
        pageTask.AssignPending(assistantId, "Do work");

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var attempt = TaskAssignmentAttempt.CreatePending(pageTask.Id, assistantId, collab.Id, 1, mangakaId);
        var grant = SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId);

        _attemptRepo.Setup(r => r.GetByIdAsync(attemptId, It.IsAny<CancellationToken>())).ReturnsAsync(attempt);
        _taskRepo.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pageTask);
        _chapterRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        _collabRepo.Setup(r => r.GetCollaborationAsync(collab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collab);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collab.Id, series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);

        var result = await _respondHandler.Handle(
            new RespondTaskAssignmentCommand(attemptId, true, null, assistantId, Guid.Empty), CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(PageTaskStatus.Incomplete, pageTask.TaskStatus);
        Assert.NotNull(pageTask.WorkStartedAt);
        Assert.Equal(attempt.AcceptedAt, pageTask.WorkStartedAt);
    }

    [Fact]
    public async STTask AssistantReject_SetsTaskToReassignmentRequired_AndRequiresReason()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series Title", "Desc", null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 10);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "http://image.url");
        pageTask.AssignPending(assistantId, "Do work");

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var attempt = TaskAssignmentAttempt.CreatePending(pageTask.Id, assistantId, collab.Id, 1, mangakaId);

        _attemptRepo.Setup(r => r.GetByIdAsync(attemptId, It.IsAny<CancellationToken>())).ReturnsAsync(attempt);
        _taskRepo.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pageTask);
        _chapterRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        _collabRepo.Setup(r => r.GetCollaborationAsync(collab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collab);

        // Reject without reason should throw
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _respondHandler.Handle(new RespondTaskAssignmentCommand(attemptId, false, "", assistantId, Guid.Empty), CancellationToken.None));

        // Reject with valid reason
        var result = await _respondHandler.Handle(
            new RespondTaskAssignmentCommand(attemptId, false, "Too busy with other work", assistantId, Guid.Empty), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("Too busy with other work", result.RejectionReason);
        Assert.Equal(PageTaskStatus.ReassignmentRequired, pageTask.TaskStatus);
        Assert.Null(pageTask.AssignedAssistantId);
    }

    [Fact]
    public async STTask WorkloadCapacityExceeded_ThrowsConflictException()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series Title", "Desc", null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 10);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "http://image.url");
        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var grant = SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId);

        _taskRepo.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pageTask);
        _chapterRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        _seriesRepo.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(series);
        _collabRepo.Setup(r => r.GetNonEndedCollaborationByAssistantAsync(assistantId, It.IsAny<CancellationToken>())).ReturnsAsync(collab);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collab.Id, series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);

        // Simulate workload limit reached (3 active assignments)
        _attemptRepo.Setup(r => r.GetActiveWorkloadCountAsync(assistantId, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _assignHandler.Handle(new AssignTaskToAssistantCommand(pageTask.Id, assistantId, mangakaId, "Work", null), CancellationToken.None));
    }
}
