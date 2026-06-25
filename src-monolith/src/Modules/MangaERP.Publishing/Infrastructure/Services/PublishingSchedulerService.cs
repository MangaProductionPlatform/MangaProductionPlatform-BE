using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Publishing.Application.Commands;
using System;
using System.Threading;

namespace MangaERP.Publishing.Infrastructure.Services;

public class PublishingSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PublishingSchedulerService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public PublishingSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<PublishingSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Publishing Scheduler Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndPublishAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scheduled publishing check.");
            }

            await System.Threading.Tasks.Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Publishing Scheduler Service is stopping.");
    }

    private async System.Threading.Tasks.Task PollAndPublishAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<IChapterRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Query chapters approved and whose ScheduledPublishAt has passed
        var now = DateTime.UtcNow;
        var pendingChapters = await chapterRepo.GetScheduledChaptersAsync(now, ct);

        foreach (var chapter in pendingChapters)
        {
            _logger.LogInformation("Scheduled publishing triggered for Chapter: {ChapterId} ({Title})", chapter.Id, chapter.Title);

            try
            {
                var command = new PublishChapterCommand(chapter.Id);
                var result = await mediator.Send(command, ct);

                _logger.LogInformation("Chapter: {ChapterId} successfully published automatically. Url: {Url}", result.ChapterId, result.PublicationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to automatically publish scheduled Chapter {ChapterId}.", chapter.Id);
            }
        }
    }
}
