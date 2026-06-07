using FluentValidation;
using MediatR;

namespace MangaERP.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation before the handler.
/// Returns a failure Result instead of throwing on validation errors.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));
        // Use reflection to call the static Failure method on the concrete Result type
        var failureMethod = typeof(TResponse).GetMethod("Failure",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null, new[] { typeof(string) }, null);

        if (failureMethod != null)
            return (TResponse)failureMethod.Invoke(null, new object[] { errors })!;

        throw new ValidationException(failures);
    }
}
