using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts.Messages;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.FinishDuel;

public sealed class FinishDuelHandler(Context db, IMessageSender messageSender) : IRequestHandler<FinishDuelCommand, Result>
{
    public async Task<Result> Handle(FinishDuelCommand request, CancellationToken cancellationToken)
    {
        var duel = await db.Duels.FirstOrDefaultAsync(d => d.Id == request.DuelId, cancellationToken);
        if (duel is null)
            return Result.Fail($"Duel {request.DuelId} not found");

        if (duel.Status == DuelStatus.Finished)
            return Result.Fail($"Duel {request.DuelId} is already finished");

        DuelResult result;
        if (request.Winner == "1")
        {
            result = DuelResult.User1;
        }
        else if (request.Winner == "2")
        {
            result = DuelResult.User2;
        }
        else {
            result = DuelResult.Draw;
        }
        duel.Status  = DuelStatus.Finished;
        duel.Result  = result;
        duel.EndTime = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var message = new DuelFinishedMessage
        {
            DuelId = duel.Id,
            Winner = request.Winner,
        };
        
        await messageSender.SendMessage(message, cancellationToken);

        return Result.Ok();
    }
}
