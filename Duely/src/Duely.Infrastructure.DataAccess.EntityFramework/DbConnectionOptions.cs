namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed record DbConnectionOptions
{
    public const string SectionName = "DbConnection";
    
    public string ConnectionString { get; init; } = string.Empty;
}