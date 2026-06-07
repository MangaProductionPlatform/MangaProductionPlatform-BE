using MediatR;
using Microsoft.Extensions.Logging;

namespace MangaERP.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request and response details.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("[MangaERP] Handling {RequestName}", requestName);

        var response = await next();

        _logger.LogInformation("[MangaERP] Handled {RequestName}", requestName);
        return response;
    }
}
