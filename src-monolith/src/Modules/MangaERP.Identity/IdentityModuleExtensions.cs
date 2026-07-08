using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Identity.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MangaERP.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Framework services
        services.AddMemoryCache();

        // MediatR — scan this assembly for all handlers
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // FluentValidation — auto-register all validators in this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Application → Infrastructure port wiring
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddHttpClient(); // Required by BrevoEmailService
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddScoped<IUsernameGenerator, UsernameGeneratorService>();

        return services;
    }
}
