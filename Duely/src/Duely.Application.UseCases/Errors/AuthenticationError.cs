using FluentResults;

namespace Duely.Application.UseCases.Errors;

public sealed class AuthenticationError : Error
{
    public AuthenticationError()
        : base("Authentication error")
    {
    }
}