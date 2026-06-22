using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class EntityNotFoundError(string message) : Error(message);
