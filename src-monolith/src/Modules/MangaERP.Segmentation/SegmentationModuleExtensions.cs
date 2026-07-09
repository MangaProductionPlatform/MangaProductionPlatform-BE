using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Infrastructure;
using MangaERP.Segmentation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaERP.Segmentation;

public static class SegmentationModuleExtensions
{
    public static IServiceCollection AddSegmentationModule(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        var parsedConnectionString = BuildPostgresConnectionString(connectionString);

        services.AddDbContext<SegmentationDbContext>(options =>
            options.UseNpgsql(
                parsedConnectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__SegmentationMigrationsHistory")
                    .EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)));

        services.AddScoped<ISegmentationTaskRepository, SegmentationTaskRepository>();

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
