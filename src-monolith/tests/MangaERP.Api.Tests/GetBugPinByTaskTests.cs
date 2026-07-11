using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Application.Queries;
using MangaERP.QA.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class GetBugPinByTaskTests
{
    private readonly Mock<IBugPinRepository> _bugPinRepo = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepo = new();
    private readonly Mock<IChapterRepository> _chapterRepo = new();
    private readonly Mock<ISeriesRepository> _seriesRepo = new();

    [Fact]
    public async STTask Handle_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var context = SetUpTask();
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new GetBugPinByTaskQuery(context.PageTask.Id, Guid.NewGuid()),
            CancellationToken.None));

        _bugPinRepo.Verify(
            repository => repository.GetByPageTaskIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async STTask Handle_OnlyClosedPins_ReturnsNull()
    {
        var context = SetUpTask();
        _bugPinRepo.Setup(repository => repository.GetByPageTaskIdAsync(
                context.PageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePin(context, "Fixed", DateTime.UtcNow),
                CreatePin(context, "Resolved", DateTime.UtcNow.AddMinutes(-1))
            });

        var result = await CreateHandler().Handle(
            new GetBugPinByTaskQuery(context.PageTask.Id, context.AssistantId),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async STTask Handle_ActivePins_ReturnsNewestActivePin()
    {
        var context = SetUpTask();
        var expected = CreatePin(context, "InFixing", DateTime.UtcNow);
        _bugPinRepo.Setup(repository => repository.GetByPageTaskIdAsync(
                context.PageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePin(context, "Open", DateTime.UtcNow.AddMinutes(-1)),
                expected,
                CreatePin(context, "Fixed", DateTime.UtcNow.AddMinutes(1))
            });

        var result = await CreateHandler().Handle(
            new GetBugPinByTaskQuery(context.PageTask.Id, context.AssistantId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        Assert.Equal("InFixing", result.Status);
    }

    [Fact]
    public async STTask Handle_SeriesAuthor_CanViewActivePin()
    {
        var context = SetUpTask();
        var pin = CreatePin(context, "Open", DateTime.UtcNow);
        _bugPinRepo.Setup(repository => repository.GetByPageTaskIdAsync(
                context.PageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pin });

        var result = await CreateHandler().Handle(
            new GetBugPinByTaskQuery(context.PageTask.Id, context.AuthorId),
            CancellationToken.None);

        Assert.Equal(pin.Id, result?.Id);
    }

    private GetBugPinByTaskHandler CreateHandler() => new(
        _bugPinRepo.Object,
        _pageTaskRepo.Object,
        _chapterRepo.Object,
        _seriesRepo.Object);

    private TestContext SetUpTask()
    {
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 1);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page-1.png");
        pageTask.Activate(assistantId);

        _pageTaskRepo.Setup(repository => repository.GetByIdAsync(
                pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepo.Setup(repository => repository.GetByIdAsync(
                chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepo.Setup(repository => repository.GetByIdAsync(
                series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        return new TestContext(authorId, assistantId, pageTask, chapter);
    }

    private static BugPin CreatePin(TestContext context, string status, DateTime createdAt) => new()
    {
        ChapterId = context.Chapter.Id,
        PageTaskId = context.PageTask.Id,
        EditorId = Guid.NewGuid(),
        NoteMessage = "Fix this issue",
        Status = status,
        CreatedAt = createdAt
    };

    private sealed record TestContext(
        Guid AuthorId,
        Guid AssistantId,
        PageTask PageTask,
        ChapterEntity Chapter);
}
