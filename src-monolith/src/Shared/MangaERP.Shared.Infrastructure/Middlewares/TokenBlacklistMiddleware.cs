using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MangaERP.Shared.Application.Ports;
using Microsoft.AspNetCore.Http;
using Task = System.Threading.Tasks.Task;

namespace MangaERP.Shared.Infrastructure.Middlewares;

public class TokenBlacklistMiddleware : IMiddleware
{
    private readonly ITokenBlacklistService _blacklistService;

    public TokenBlacklistMiddleware(ITokenBlacklistService blacklistService) =>
        _blacklistService = blacklistService;

    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Obtain JTI claim from context.User populated during Authentication phase
        var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value 
                  ?? context.User.FindFirst("jti")?.Value;

        if (!string.IsNullOrEmpty(jti) && _blacklistService.IsBlacklisted(jti))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "TokenRevoked",
                message = "Phiên làm việc đã bị thu hồi hoặc bạn đã đăng xuất. Vui lòng đăng nhập lại."
            });
            return;
        }

        await next(context);
    }
}
