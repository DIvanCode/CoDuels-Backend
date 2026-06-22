using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class ForbiddenError(string message) : Error(message)
{
    public ForbiddenError(): this("У вас нет прав на выполнение операции с этим ресурсом.")
    {
    }
}
