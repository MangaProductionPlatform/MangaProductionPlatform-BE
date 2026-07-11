using MangaERP.Chapter.Application.Queries.GetChapterPages;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class GetChapterPagesTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();

    private readonly GetChapterPagesHandler _handler;

    public GetChapterPagesTests()
    {
        _handler = new GetChapterPagesHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);
    }

    [Fact]
    public async STTask Handle_ChapterNotFound_ThrowsKeyNotFoundException()
    {
        var chapterId = Guid.NewGuid();
        var query = new GetChapterPagesQuery(Guid.NewGuid(), chapterId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterEntity)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_ReturnsPages()
    {
        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var page1 = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page-1.png");
        var page2 = PageTask.CreatePending(chapter.Id, 2, "https://example.com/page-2.png");

        var query = new GetChapterPagesQuery(authorId, chapter.Id);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask> { page1, page2 });

        var result = (await _handler.Handle(query, CancellationToken.None)).ToList();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(page1.Id, result[0].PageTaskId);
        Assert.Equal(page2.Id, result[1].PageTaskId);
    }

    [Fact]
    public async STTask Handle_AssignedEditor_ReturnsPages()
    {
        var authorId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3, editorId);
        var page1 = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page-1.png");
        var query = new GetChapterPagesQuery(editorId, chapter.Id);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask> { page1 });

        var result = (await _handler.Handle(query, CancellationToken.None)).ToList();

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(page1.Id, result[0].PageTaskId);
    }

    [Fact]
    public async STTask Handle_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3, Guid.NewGuid());
        var query = new GetChapterPagesQuery(Guid.NewGuid(), chapter.Id);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(query, CancellationToken.None));
    }
}
