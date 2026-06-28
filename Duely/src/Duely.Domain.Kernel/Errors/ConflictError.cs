using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class ConflictError(string message) : Error(message);
