using MangaERP.Chapter.Application.Commands.UpdateTaskDeadline;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class UpdateTaskDeadlineTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();

    private readonly UpdateTaskDeadlineHandler _handler;

    public UpdateTaskDeadlineTests()
    {
        _handler = new UpdateTaskDeadlineHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);
    }

    [Fact]
    public async STTask Handle_TaskNotFound_ThrowsKeyNotFoundException()
    {
        var taskId = Guid.NewGuid();
        var command = new UpdateTaskDeadlineCommand(Guid.NewGuid(), taskId, DateTime.UtcNow.AddDays(1));

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageTask)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedMangaka_ThrowsUnauthorizedAccessException()
    {
        var authorId = Guid.NewGuid();
        var wrongMangakaId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1);

        var command = new UpdateTaskDeadlineCommand(wrongMangakaId, pageTask.Id, DateTime.UtcNow.AddDays(1));

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_UpdatesDeadlineSuccessfully()
    {
        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1);
        var deadline = DateTime.UtcNow.AddDays(5);

        var command = new UpdateTaskDeadlineCommand(authorId, pageTask.Id, deadline);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(pageTask.Id, result.PageTaskId);
        Assert.Equal(deadline, result.Deadline);
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
