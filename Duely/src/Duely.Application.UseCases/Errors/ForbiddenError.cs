using FluentResults;

namespace Duely.Application.UseCases.Errors;

public sealed class ForbiddenError : Error
{
    public ForbiddenError(string message)
        : base(message)
    {
    }
}