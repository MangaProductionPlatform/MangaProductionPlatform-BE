using FluentValidation;
using MangaERP.Shared.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Submission;

public static class SubmissionModuleExtensions
{
    public static IServiceCollection AddSubmissionModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers + ValidationBehavior pipeline for this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators for this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
