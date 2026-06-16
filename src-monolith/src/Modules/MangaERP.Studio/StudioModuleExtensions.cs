using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Studio;

public static class StudioModuleExtensions
{
    public static IServiceCollection AddStudioModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers for this assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators for this assembly
        services.AddValidatorsFromAssembly(assembly);

        // NOTE: IStudioInvitationRepository and IStudioIdentityService are registered
        // in SharedInfrastructureExtensions to avoid circular project references.

        return services;
    }
}
