using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MangaERP.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps commands inside a DB transaction.
/// Only applies to ICommand requests, not queries.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly DbContext _dbContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(DbContext dbContext, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands in transactions (not queries)
        var requestName = typeof(TRequest).Name;
        if (!requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
            return await next();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _logger.LogDebug("[MangaERP] Beginning transaction for {RequestName}", requestName);
            var response = await next();

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("[MangaERP] Committed transaction for {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MangaERP] Rolling back transaction for {RequestName}", requestName);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
