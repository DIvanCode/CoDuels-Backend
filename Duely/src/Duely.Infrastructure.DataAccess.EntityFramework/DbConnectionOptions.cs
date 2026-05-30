namespace Duely.Infrastructure.DataAccess.EntityFramework;

internal sealed record DbConnectionOptions
{
    public const string SectionName = "DbConnection";
    
    public required string ConnectionString { get; init; }
}
