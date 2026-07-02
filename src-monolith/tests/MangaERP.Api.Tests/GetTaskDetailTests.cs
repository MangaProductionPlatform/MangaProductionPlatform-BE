using MangaERP.Chapter.Application.Queries.GetTaskDetail;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class GetTaskDetailTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();

    private readonly GetTaskDetailHandler _handler;

    public GetTaskDetailTests()
    {
        _handler = new GetTaskDetailHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);
    }

    [Fact]
    public async STTask Handle_TaskNotFound_ThrowsKeyNotFoundException()
    {
        var taskId = Guid.NewGuid();
        var query = new GetTaskDetailQuery(Guid.NewGuid(), taskId);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageTask)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var authorId = Guid.NewGuid();
        var userNotOwner = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1);

        var query = new GetTaskDetailQuery(userNotOwner, pageTask.Id);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_ReturnsTaskDetails()
    {
        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1);
        pageTask.SetDeadline(DateTime.UtcNow.AddDays(5));

        var query = new GetTaskDetailQuery(authorId, pageTask.Id);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(pageTask.Id, result.PageTaskId);
        Assert.NotNull(result.Deadline);
    }
}
