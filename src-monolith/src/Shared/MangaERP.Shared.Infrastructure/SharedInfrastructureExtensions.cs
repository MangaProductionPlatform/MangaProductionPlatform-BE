using MangaERP.Chapter.Application.Ports;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Services;
using MangaERP.Submission.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.QA.Application.Ports;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Task.Application.Ports;
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
        /*
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));
        */

        // [SQL SERVER CONFIG] - Dùng để test local với SSMS
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sqlServer => sqlServer.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));
        // Allow all modules to access AppDbContext without a direct circular reference
        services.AddScoped<IDbContextProvider, AppDbContextProvider>();

        // Shared Repositories for modules
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ISeriesRepository, SeriesRepository>();

        // Chapter module infrastructure
        services.AddScoped<ChapterRepository>();
        services.AddScoped<IChapterRepository>(sp => sp.GetRequiredService<ChapterRepository>());
        services.AddScoped<IPageTaskRepository>(sp => sp.GetRequiredService<ChapterRepository>());

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

        // Chapter / Task / Notification infrastructure (MF2)
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<IPageTaskRepository, PageTaskRepository>();
        services.AddScoped<IPreviewPageRepository, PreviewPageRepository>();
        services.AddScoped<IArtworkLayerRepository, ArtworkLayerRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
