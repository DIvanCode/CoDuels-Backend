using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class AuthenticationError(string message) : Error(message);
