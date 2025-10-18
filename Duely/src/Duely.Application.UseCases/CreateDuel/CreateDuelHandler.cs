using Duely.Domain.Models;
using MediatR;
using FluentResults;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.Configuration;

namespace Duely.Application.UseCases.CreateDuel;

public class CreateDuelHandler(ITaskiClient taskiClient, Context db, IMessageSender messageSender, IOptions<DuelSettings> options) : IRequestHandler<CreateDuelCommand, Result>
{

    public async Task<Result> Handle(CreateDuelCommand request, CancellationToken cancellationToken)
    {
        var taskResult = await taskiClient.GetRandomTaskIdAsync(cancellationToken);

        if (taskResult.IsFailed)
        {
            return Result.Fail("Failed to get random task");
        }

        var taskId = taskResult.Value;

        var startTime = DateTime.UtcNow;
        var maxDuration = options.Value.MaxDurationMinutes;

        var duel = new Duel 
        {
            TaskId = taskId,
            User1Id = request.User1Id,
            User2Id = request.User2Id,
            Status = DuelStatus.InProgress,
            Result = DuelResult.None,
            StartTime = startTime,
            MaxDuration = maxDuration,
        };

        db.Duels.Add(duel);
        await db.SaveChangesAsync(cancellationToken);

        var message = new DuelStartedMessage
        {
            DuelId = duel.Id,
        };

        await messageSender.SendMessage(message, cancellationToken);

        return Result.Ok();
        
    }
}