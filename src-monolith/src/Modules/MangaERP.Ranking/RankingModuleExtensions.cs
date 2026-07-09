using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Infrastructure.Services;

namespace MangaERP.Ranking;

public static class RankingModuleExtensions
{
    public static IServiceCollection AddRankingModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers in this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators in this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register services
        services.AddScoped<IRankingCalculator, RankingCalculator>();

        // Register Background Job
        services.AddHostedService<RankingRefreshJob>();

        return services;
    }
}
