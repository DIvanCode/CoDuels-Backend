using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class EntityAlreadyExistsError(string message) : Error(message);
