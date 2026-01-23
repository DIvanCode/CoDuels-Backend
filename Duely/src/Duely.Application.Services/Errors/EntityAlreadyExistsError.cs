using FluentResults;

namespace Duely.Application.Services.Errors;

public sealed class EntityAlreadyExistsError : Error
{
    public EntityAlreadyExistsError(string message)
        : base(message)
    {
    }
    
    public EntityAlreadyExistsError(string type, string condition)
        : base($"'{type}' with {condition} already exists.")
    {
    }
    
    public EntityAlreadyExistsError(string type, string field, object value)
        : base($"'{type}' with '{field}' = '{value}' already exists.")
    {
    }
}