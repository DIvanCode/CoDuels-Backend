using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result> { }

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    ITaskiClient taskiClient,
    IMessageSender messageSender,
    IOptions<DuelOptions> duelOptions,
    Context context)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pair = duelManager.TryGetPair();

        if (pair is null)
        {
            return Result.Ok();
        }

        var user1 = await context.Users.SingleOrDefaultAsync(u => u.Id == pair.Value.User1, cancellationToken);
        if (user1 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), pair.Value.User1);
        }

        var user2 = await context.Users.SingleOrDefaultAsync(u => u.Id == pair.Value.User2, cancellationToken);
        if (user2 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), pair.Value.User2);
        }

        var taskResult = await taskiClient.GetRandomTaskIdAsync(cancellationToken);
        if (taskResult.IsFailed)
        {
            return Result.Fail("Failed to get random task");
        }

        var startTime = DateTime.UtcNow;
        var deadlineTime = startTime.AddMinutes(duelOptions.Value.MaxDurationMinutes);

        var duel = new Duel
        {
            TaskId = taskResult.Value,
            User1 = user1,
            User2 = user2,
            Status = DuelStatus.InProgress,
            StartTime = startTime,
            DeadlineTime = deadlineTime
        };

        context.Duels.Add(duel);
        await context.SaveChangesAsync(cancellationToken);

        var message = new DuelStartedMessage
        {
            DuelId = duel.Id,
        };

        await messageSender.SendMessage(duel.User1.Id, message, cancellationToken);
        await messageSender.SendMessage(duel.User2.Id, message, cancellationToken);

        return Result.Ok();
    }
}