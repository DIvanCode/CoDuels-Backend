using FluentResults;

namespace Duely.Domain.Kernel.Errors;

public class ValidationError : Error
{
    public ValidationError(string message) : base(message)
    {
    }

    public ValidationError(List<string> errors) : base("Ошибка валидации.")
    {
        if (errors.Count == 0)
        {
            return;
        }
        
        Message = $"{string.Join("\n", errors)}";
    }
}
