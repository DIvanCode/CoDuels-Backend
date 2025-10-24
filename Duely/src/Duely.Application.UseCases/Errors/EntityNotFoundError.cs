using FluentResults;

namespace Duely.Application.UseCases.Errors;

public sealed class EntityNotFoundError : Error
{
    public EntityNotFoundError(string message)
        : base(message)
    {
    }
    
    public EntityNotFoundError(string type, string condition)
        : base($"'{type}' with {condition} does not exist.")
    {
    }

    public EntityNotFoundError(string type, string field, object value)
        : base($"'{type}' with '{field}' = '{value}' does not exist.")
    {
    }
}