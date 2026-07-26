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
using MangaERP.Ranking.Application.Ports;
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
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                parsedConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));

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

        // Studio module infrastructure
        services.AddScoped<IStudioInvitationRepository, StudioInvitationRepository>();
        services.AddScoped<ISeriesAccessGrantRepository, SeriesAccessGrantRepository>();
        services.AddScoped<ICollaborationAuthorizationService, CollaborationAuthorizationService>();
        services.AddScoped<IStudioIdentityService, StudioIdentityService>();
        services.AddScoped<IStudioTaskRevocationService, StudioTaskRevocationService>();

        // Chapter / Task / Notification infrastructure
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<IPageTaskRepository, PageTaskRepository>();
        services.AddScoped<IPreviewPageRepository, PreviewPageRepository>();
        services.AddScoped<IArtworkLayerRepository, ArtworkLayerRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<IDeadlineExtensionRequestRepository, DeadlineExtensionRequestRepository>();
        services.AddScoped<ITaskAssignmentAttemptRepository, TaskAssignmentRepository>();
        services.AddScoped<ITaskProgressRepository, TaskProgressRepository>();
        services.AddScoped<ITaskCheckpointRepository, TaskCheckpointRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();

        // Ranking module infrastructure
        services.AddScoped<IRankingRepository, RankingRepository>();

        // Shared Services
        services.AddScoped<INotificationService, NotificationService>();

        // Task deadline monitor background services
        services.AddHostedService<TaskDeadlineMonitorService>();
        services.AddHostedService<HalfwayDeadlineWarningBackgroundService>();

        // Infrastructure Reliability & Security services
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        services.AddTransient<GlobalExceptionMiddleware>();
        services.AddTransient<TokenBlacklistMiddleware>();

        return services;
    }

    private static string? BuildPostgresConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        
        if (!connectionString.StartsWith("postgres://") && !connectionString.StartsWith("postgresql://"))
        {
            return connectionString;
        }

        try
        {
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
            return connectionString;
        }
    }
}
