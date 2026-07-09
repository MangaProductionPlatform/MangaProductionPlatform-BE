using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;

namespace MangaERP.Ranking.Infrastructure.Services;

public class RankingRefreshJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RankingRefreshJob> _logger;
    private readonly int _refreshIntervalMinutes;
    private readonly int _maxItems;

    public RankingRefreshJob(IServiceProvider serviceProvider, ILogger<RankingRefreshJob> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _refreshIntervalMinutes = configuration.GetValue<int>("Ranking:RefreshIntervalMinutes", 360); // Default 6 hours
        _maxItems = configuration.GetValue<int>("Ranking:MaxItems", 100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RankingRefreshJob is starting.");

        // Wait 5 minutes before the first run to allow the application to start up properly
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RankingRefreshJob is running.");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var calculator = scope.ServiceProvider.GetRequiredService<IRankingCalculator>();
                var rankingRepo = scope.ServiceProvider.GetRequiredService<IRankingRepository>();

                var periods = new[] { RankingPeriod.Daily, RankingPeriod.Weekly, RankingPeriod.Monthly, RankingPeriod.AllTime };
                
                foreach (var period in periods)
                {
                    _logger.LogInformation("Calculating rankings for period: {Period}", period);
                    var newSnapshots = await calculator.CalculateAsync(period, _maxItems, stoppingToken);
                    await rankingRepo.ReplaceSnapshotAsync(period, newSnapshots, stoppingToken);
                }

                await rankingRepo.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("RankingRefreshJob completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing RankingRefreshJob.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_refreshIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("RankingRefreshJob is stopping.");
    }
}
