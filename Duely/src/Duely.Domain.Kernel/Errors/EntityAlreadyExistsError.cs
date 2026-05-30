using FluentResults;

namespace Duely.Domain.Common.Errors;

public class EntityAlreadyExistsError(string message) : Error(message);
