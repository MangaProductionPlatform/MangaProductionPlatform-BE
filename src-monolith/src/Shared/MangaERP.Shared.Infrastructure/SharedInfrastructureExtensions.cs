using MangaERP.Chapter.Application.Ports;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Services;
using MangaERP.Submission.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.QA.Application.Ports;
using MangaERP.Task.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MangaERP.Shared.Infrastructure.Middlewares;

namespace MangaERP.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        var parsedConnectionString = BuildPostgresConnectionString(connectionString);

        // ── DATABASE CONFIGURATION ──────────────────────────────────────────────────
        // [POSTGRESQL CONFIG] - Mặc định khi deploy (Hãy uncomment dòng này khi deploy)
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                parsedConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));

        // [SQL SERVER CONFIG] - Dùng để test local với SSMS
        /*
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

        // QA module infrastructure
        services.AddScoped<QARepositories>();
        services.AddScoped<IBugPinRepository>(sp => sp.GetRequiredService<QARepositories>());
        services.AddScoped<IQASessionRepository>(sp => sp.GetRequiredService<QARepositories>());

        // Publishing module infrastructure
        services.AddScoped<PublishingRepositories>();
        services.AddScoped<IPublicationRecordRepository>(sp => sp.GetRequiredService<PublishingRepositories>());
        services.AddScoped<INotificationRepository>(sp => sp.GetRequiredService<PublishingRepositories>());

        // Studio module infrastructure (here to avoid circular dependency)
        services.AddScoped<IStudioInvitationRepository, StudioInvitationRepository>();
        services.AddScoped<IStudioIdentityService, StudioIdentityService>();
        services.AddScoped<IStudioTaskRevocationService, NoOpStudioTaskRevocationService>();

        // Chapter / Task / Notification infrastructure (MF2)
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<IPageTaskRepository, PageTaskRepository>();
        services.AddScoped<IPreviewPageRepository, PreviewPageRepository>();
        services.AddScoped<IArtworkLayerRepository, ArtworkLayerRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();

        // Shared Services
        services.AddScoped<INotificationService, NotificationService>();

        // Infrastructure Reliability & Security services
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        services.AddTransient<GlobalExceptionMiddleware>();
        services.AddTransient<TokenBlacklistMiddleware>();

        return services;
    }

    private static string? BuildPostgresConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        
        // If it's already a standard ADO.NET string (contains "="), just return it
        if (!connectionString.StartsWith("postgres://") && !connectionString.StartsWith("postgresql://"))
        {
            return connectionString;
        }

        try
        {
            // Parse Uri format: postgres://username:password@host:port/database
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? userInfo[0] : "";
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var database = uri.AbsolutePath.TrimStart('/');
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            
            return $"Host={host};Database={database};Username={username};Password={password};Port={port};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            return connectionString; // fallback to original if parsing fails
        }
    }
}
