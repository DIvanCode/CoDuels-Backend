using FluentResults;

namespace Duely.Application.Services.Errors;

public sealed class AuthenticationError : Error
{
    public AuthenticationError()
        : base("Authentication error")
    {
    }
}