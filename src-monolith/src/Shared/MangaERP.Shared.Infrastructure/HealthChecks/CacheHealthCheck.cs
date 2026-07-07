using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MangaERP.Shared.Infrastructure.HealthChecks;

public class CacheHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;

    public CacheHealthCheck(IMemoryCache cache) => _cache = cache;

    public System.Threading.Tasks.Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var testKey = "__healthcheck__";
            _cache.Set(testKey, "ok", TimeSpan.FromSeconds(1));
            var isHealthy = _cache.TryGetValue(testKey, out _);

            return System.Threading.Tasks.Task.FromResult(isHealthy
                ? HealthCheckResult.Healthy("Memory cache is responsive.")
                : HealthCheckResult.Unhealthy("Memory cache read/write failed."));
        }
        catch (Exception ex)
        {
            return System.Threading.Tasks.Task.FromResult(HealthCheckResult.Unhealthy("Memory cache check threw an exception.", ex));
        }
    }
}
