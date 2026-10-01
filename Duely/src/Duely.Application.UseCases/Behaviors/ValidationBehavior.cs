using FluentValidation;
using MediatR;

namespace Duely.Application.UseCases.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var errors = results.SelectMany(result => result.Errors).ToList();
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await next(cancellationToken);
    }
}
