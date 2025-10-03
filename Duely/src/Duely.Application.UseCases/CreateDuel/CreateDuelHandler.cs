using Duely.Domain.Models;
using MediatR;
using FluentResults;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using System.Data.Common;

namespace Duely.Application.UseCases.CreateDuel;

public class CreateDuelHandler(ITaskiClient taskiClient, Context db) : IRequestHandler<CreateDuelCommand, Result>
{

    public async Task<Result> Handle(CreateDuelCommand request, CancellationToken cancellationToken)
    {
        var taskResult = await taskiClient.GetRandomTaskIdAsync(cancellationToken);

        if (taskResult.IsFailed)
        {
            return Result.Fail("Failed to get random task");
        }

        var taskId = taskResult.Value;

        var duel = new Duel 
        {
            TaskId = taskId,
            User1Id = request.User1Id,
            User2Id = request.User2Id,
            Status = DuelStatus.InProgress,
            Result = DuelResult.None,
            StartTime = DateTime.UtcNow
        };

        db.Duels.Add(duel);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
        
    }
}