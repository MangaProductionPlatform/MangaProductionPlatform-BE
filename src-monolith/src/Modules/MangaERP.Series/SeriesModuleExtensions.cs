using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Series;

public static class SeriesModuleExtensions
{
    public static IServiceCollection AddSeriesModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers for this assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators for this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
