using FluentResults;

namespace Duely.Domain.Common.Errors;

public class EntityNotFoundError(string message) : Error(message);
