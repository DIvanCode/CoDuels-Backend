using Duely.Domain.Models;
using MediatR;
using FluentResults;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CreateDuelCommand : IRequest<Result>
{
    public required int User1Id { get; init; }
    public required int User2Id { get; init; }
}

public sealed class CreateDuelHandler(
    Context context,
    ITaskiClient taskiClient,
    IMessageSender messageSender,
    IOptions<DuelOptions> duelOptions)
    : IRequestHandler<CreateDuelCommand, Result>
{
    public async Task<Result> Handle(CreateDuelCommand command, CancellationToken cancellationToken)
    {
        var user1 = await context.Users.SingleOrDefaultAsync(u => u.Id == command.User1Id, cancellationToken);
        if (user1 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.User1Id);
        }

        var user2 = await context.Users.SingleOrDefaultAsync(u => u.Id == command.User2Id, cancellationToken);
        if (user2 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.User2Id);
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
