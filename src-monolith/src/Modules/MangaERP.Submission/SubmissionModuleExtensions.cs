using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Submission;

public static class SubmissionModuleExtensions
{
    public static IServiceCollection AddSubmissionModule(this IServiceCollection services)
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
