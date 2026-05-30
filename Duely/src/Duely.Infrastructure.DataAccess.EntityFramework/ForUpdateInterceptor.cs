using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

internal sealed class ForUpdateInterceptor : DbCommandInterceptor
{
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData, 
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(QueryExtensions.ForUpdateTag))
        {
            command.CommandText += " FOR UPDATE";
        }
        
        return result;
    }
}
