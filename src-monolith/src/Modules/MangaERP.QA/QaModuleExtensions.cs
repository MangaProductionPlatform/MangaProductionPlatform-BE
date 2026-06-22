using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.QA;

public static class QaModuleExtensions
{
    public static IServiceCollection AddQaModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers in this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators in this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
