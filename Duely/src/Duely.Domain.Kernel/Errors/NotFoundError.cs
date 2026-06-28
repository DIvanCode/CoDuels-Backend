using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class NotFoundError(string message) : Error(message);
