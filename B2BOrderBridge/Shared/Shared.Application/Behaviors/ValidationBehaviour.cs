using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Shared.Application.Behaviors;

public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<ValidationFailure>();
        // Validators may share a scoped DbContext: do not execute them concurrently.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors);
        }
        if (failures.Count != 0)
            throw new ValidationException(failures);
        return await next(cancellationToken);
    }
}
