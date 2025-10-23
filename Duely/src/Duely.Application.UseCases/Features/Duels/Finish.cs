using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Domain.Models.Messages;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class FinishDuelCommand : IRequest<Result>
{
    public required int DuelId { get; init; }
    public required int? WinnerId { get; init; }
}

public sealed class FinishDuelHandler(Context context, IMessageSender messageSender)
    : IRequestHandler<FinishDuelCommand, Result>
{
    public async Task<Result> Handle(FinishDuelCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels.SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (duel.Status == DuelStatus.Finished)
        {
            return Result.Fail($"Duel {command.DuelId} is already finished");
        }

        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;

        if (command.WinnerId is not null)
        {
            var winner = await context.Users.SingleOrDefaultAsync(u => u.Id == command.WinnerId, cancellationToken);
            if (winner is null)
            {
                return new EntityNotFoundError(nameof(User), nameof(User.Id), command.WinnerId);
            }

            duel.Winner = winner;
        }

        await context.SaveChangesAsync(cancellationToken);

        var message = new DuelFinishedMessage
        {
            DuelId = duel.Id,
        };

        await messageSender.SendMessage(message, cancellationToken);

        return Result.Ok();
    }
}
