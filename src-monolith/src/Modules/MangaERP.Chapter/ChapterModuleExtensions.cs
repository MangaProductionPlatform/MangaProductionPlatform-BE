using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Chapter;

public static class ChapterModuleExtensions
{
    public static IServiceCollection AddChapterModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
