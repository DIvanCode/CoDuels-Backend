using FluentResults;

namespace Duely.Application.Services.Errors;

public sealed class RateLimitExceededError : Error
{
    public RateLimitExceededError(string message)
        : base(message)
    {
        
    }
}