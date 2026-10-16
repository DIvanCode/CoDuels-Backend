using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RecordUserPresenceCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required DateTime OccurredAt { get; init; }
}

public sealed class RecordUserPresenceHandler(Context context)
    : IRequestHandler<RecordUserPresenceCommand, Result>
{
    public async Task<Result> Handle(
        RecordUserPresenceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OccurredAt.Kind != DateTimeKind.Utc)
        {
            return Result.Fail("Presence time must be UTC.");
        }

        // A single conditional UPDATE keeps delayed connect/disconnect writes from
        // overwriting a newer event, including when requests run in other scopes.
        await context.Users
            .Where(user => user.Id == command.UserId &&
                (user.LastSeenAt == null || user.LastSeenAt < command.OccurredAt))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.LastSeenAt, command.OccurredAt),
                cancellationToken);

        return Result.Ok();
    }
}
