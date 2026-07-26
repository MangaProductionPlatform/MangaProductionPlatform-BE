using MediatR;
using MangaERP.Task.Application.Commands.CheckHalfwayDeadlineWarnings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Shared.Infrastructure.Services;

public class HalfwayDeadlineWarningBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<HalfwayDeadlineWarningBackgroundService> _logger;

    public HalfwayDeadlineWarningBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<HalfwayDeadlineWarningBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool enabled = _config.GetValue<bool?>("TaskDeadlineWarnings:Enabled") ?? true;
        int intervalMinutes = _config.GetValue<int?>("TaskDeadlineWarnings:PollingIntervalMinutes") ?? 10;
        var pollingInterval = TimeSpan.FromMinutes(intervalMinutes > 0 ? intervalMinutes : 10);

        if (!enabled)
        {
            _logger.LogInformation("Halfway Deadline Warning Background Service is disabled via configuration.");
            return;
        }

        _logger.LogInformation("Halfway Deadline Warning Background Service starting with interval {IntervalMinutes}m.", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                int warningCount = await mediator.Send(new CheckHalfwayDeadlineWarningsCommand(), stoppingToken);
                if (warningCount > 0)
                {
                    _logger.LogInformation("Halfway deadline warning check completed. Sent {WarningCount} warning(s).", warningCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during halfway deadline warning execution.");
            }

            try
            {
                await System.Threading.Tasks.Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Halfway Deadline Warning Background Service is stopping.");
    }
}
