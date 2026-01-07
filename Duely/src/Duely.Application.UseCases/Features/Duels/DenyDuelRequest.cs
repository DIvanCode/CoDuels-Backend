using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class DenyDuelRequestCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class DenyDuelRequestHandler(Context context)
    : IRequestHandler<DenyDuelRequestCommand, Result>
{
    public async Task<Result> Handle(DenyDuelRequestCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (duel.Status != DuelStatus.Pending || duel.User2.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Duel), "deny", nameof(Duel.Id), command.DuelId);
        }

        context.Duels.Remove(duel);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
