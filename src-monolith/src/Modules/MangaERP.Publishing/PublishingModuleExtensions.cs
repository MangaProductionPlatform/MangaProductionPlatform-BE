using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using MangaERP.Publishing.Infrastructure.Services;

namespace MangaERP.Publishing;

public static class PublishingModuleExtensions
{
    public static IServiceCollection AddPublishingModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers in this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators in this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register Background Scheduler Service
        services.AddHostedService<PublishingSchedulerService>();

        return services;
    }
}
