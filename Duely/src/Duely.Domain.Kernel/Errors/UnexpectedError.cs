using FluentResults;

namespace Duely.Domain.Common.Errors;

public class UnexpectedError(string message) : Error(message);
 