using FluentResults;

namespace Duely.Domain.Common.Errors;

public class RateLimitExceededError(string message) : Error(message);
