using System.Runtime.InteropServices.JavaScript;
using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class InvalidOperationError(string message) : Error(message)
{
    public InvalidOperationError(): this("Некорректная операция.")
    {
    }
}
