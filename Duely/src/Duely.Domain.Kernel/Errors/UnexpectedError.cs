using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class UnexpectedError(string message) : Error(message);
