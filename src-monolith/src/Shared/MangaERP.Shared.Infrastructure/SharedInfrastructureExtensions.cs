using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Submission.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaERP.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // ── DATABASE CONFIGURATION ──────────────────────────────────────────────────
        // [POSTGRESQL CONFIG] - Mặc định khi deploy (Hãy uncomment dòng này khi deploy)
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));
        
/*
        // [SQL SERVER CONFIG] - Dùng để test local với SSMS
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sqlServer => sqlServer.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));
*/
        // Allow all modules to access AppDbContext without a direct circular reference
        services.AddScoped<IDbContextProvider, AppDbContextProvider>();

        // Shared Repositories for modules
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ISeriesRepository, SeriesRepository>();

        // Studio module infrastructure (here to avoid circular dependency)
        services.AddScoped<IStudioInvitationRepository, StudioInvitationRepository>();
        services.AddScoped<IStudioIdentityService, StudioIdentityService>();

        return services;
    }
}
