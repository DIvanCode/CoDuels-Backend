using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CancelDuelRequestCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class CancelDuelRequestHandler(Context context)
    : IRequestHandler<CancelDuelRequestCommand, Result>
{
    public async Task<Result> Handle(CancelDuelRequestCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.User1)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (duel.Status != DuelStatus.Pending || duel.User1.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Duel), "cancel", nameof(Duel.Id), command.DuelId);
        }

        context.Duels.Remove(duel);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
