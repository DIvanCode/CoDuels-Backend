using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class RateLimitExceededError(string message) : Error(message);
