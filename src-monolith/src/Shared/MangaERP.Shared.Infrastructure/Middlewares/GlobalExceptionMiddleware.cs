using System.Security.Authentication;
using FluentValidation;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace MangaERP.Shared.Infrastructure.Middlewares;

public class GlobalExceptionMiddleware : IMiddleware
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async System.Threading.Tasks.Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = MapException(exception);

        _logger.LogError(
            exception,
            "Unhandled exception: {ErrorCode} | Path: {Path} | TraceId: {TraceId}",
            errorCode, context.Request.Path, context.TraceIdentifier);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var payload = new
        {
            error = errorCode,
            message,
            traceId = context.TraceIdentifier,
            details = _env.IsDevelopment() ? exception.ToString() : null
        };

        await context.Response.WriteAsJsonAsync(payload);
    }

    private static (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "ValidationError",
                string.Join(" | ", validationEx.Errors.Select(e => e.ErrorMessage))),

            EntityNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "NotFound",
                notFoundEx.Message),

            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Bạn không có quyền thực hiện thao tác này."),

            AuthenticationException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Phiên đăng nhập không hợp lệ hoặc đã hết hạn."),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Conflict",
                conflictEx.Message),

            KeyNotFoundException notFoundKeyEx => (
                StatusCodes.Status404NotFound,
                "NotFound",
                notFoundKeyEx.Message),

            InvalidOperationException invalidOpEx => (
                StatusCodes.Status400BadRequest,
                "InvalidOperation",
                invalidOpEx.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "InternalServerError",
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
        };
    }
}
