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

        await UserLastSeenUpdater.RecordAsync(context, command.UserId, command.OccurredAt, cancellationToken);

        return Result.Ok();
    }
}
