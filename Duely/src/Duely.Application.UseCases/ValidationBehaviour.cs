using Duely.Domain.Kernel.Errors;
using FluentResults;
using FluentValidation;
using MediatR;

namespace Duely.Application.UseCases;

internal sealed class ValidationBehavior<TCommand, TResponse>(IValidator<TCommand>? validator = null)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TCommand request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validator is null)
        {
            return await next(cancellationToken);
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (validationResult.IsValid)
        {
            return await next(cancellationToken);
        }

        var validationError = new ValidationError(validationResult.Errors
            .Select(x => x.ErrorMessage)
            .ToList());

        // Little hack to shut the compiler since we know that ValidationError can be cast to TResponse
        return (dynamic)validationError;
    }
}