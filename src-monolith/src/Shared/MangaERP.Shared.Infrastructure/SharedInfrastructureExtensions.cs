using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Submission.Application.Ports;
using MangaERP.Series.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaERP.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));

        // Allow all modules to access AppDbContext without a direct circular reference
        services.AddScoped<IDbContextProvider, AppDbContextProvider>();

        // Shared Repositories for modules
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ISeriesRepository, SeriesRepository>();

        return services;
    }
}
