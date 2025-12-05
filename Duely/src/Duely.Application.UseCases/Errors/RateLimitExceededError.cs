using FluentResults;

namespace Duely.Application.UseCases.Errors;

public sealed class RateLimitExceededError : Error
{
    public RateLimitExceededError(string message)
        : base(message)
    {
        
    }
}