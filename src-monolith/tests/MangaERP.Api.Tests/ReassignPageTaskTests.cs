using MangaERP.Chapter.Application.Commands.ReassignPageTask;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class ReassignPageTaskTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IStudioInvitationRepository> _studioRepoMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private readonly ReassignPageTaskHandler _handler;

    public ReassignPageTaskTests()
    {
        _handler = new ReassignPageTaskHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object,
            _userRepoMock.Object,
            _studioRepoMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public async STTask Handle_ChapterNotFound_ThrowsKeyNotFoundException()
    {
        var chapterId = Guid.NewGuid();
        var command = new ReassignPageTaskCommand(Guid.NewGuid(), chapterId, 1, Guid.NewGuid());

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterEntity)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_PageTaskNotFound_ThrowsKeyNotFoundException()
    {
        var authorId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        
        var command = new ReassignPageTaskCommand(authorId, chapter.Id, 1, newAssistantId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumberAsync(chapter.Id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageTask)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_Success()
    {
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page-1.png");
        pageTask.Activate(assistantId, PageTaskType.General, "Initial desc"); // Set status to Incomplete to allow reassign

        var newAssistantUser = new User { Id = newAssistantId, Role = UserRole.Assistant };
        var studioInvitation = new StudioInvitation
        {
            AssistantUserId = newAssistantId,
            SeriesId = series.Id,
            Status = StudioInvitationStatus.Accepted
        };

        var command = new ReassignPageTaskCommand(authorId, chapter.Id, 1, newAssistantId, "Reassigned desc");

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumberAsync(chapter.Id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _userRepoMock.Setup(r => r.GetByIdAsync(newAssistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newAssistantUser);
        _studioRepoMock.Setup(r => r.GetBySeriesIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioInvitation> { studioInvitation });

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(newAssistantId, result.AssignedAssistantId);
        Assert.Equal("Reassigned desc", result.Description);
        Assert.Equal(PageTaskStatus.Incomplete.ToString(), result.TaskStatus);

        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyTaskAssignedAsync(newAssistantId, pageTask.Id, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
