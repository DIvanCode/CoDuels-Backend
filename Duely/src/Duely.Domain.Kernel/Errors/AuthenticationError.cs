using FluentResults;

namespace Duely.Domain.Common.Errors;

public class AuthenticationError(string message) : Error(message);
