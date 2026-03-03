using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentValidation;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class UpdateDuelTaskSolutionCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
    public required char TaskKey { get; init; }
    public required string Solution { get; init; }
    public required Language Language { get; init; }
}

public sealed class UpdateDuelTaskSolutionHandler(Context context)
    : IRequestHandler<UpdateDuelTaskSolutionCommand, Result>
{
    public async Task<Result> Handle(UpdateDuelTaskSolutionCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Configuration)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (!duel.Tasks.ContainsKey(command.TaskKey))
        {
            return new EntityNotFoundError(nameof(DuelTask), "key", command.TaskKey.ToString());
        }

        int opponentId;
        if (duel.User1.Id == command.UserId)
        {
            if (!duel.User1Solutions.TryGetValue(command.TaskKey, out var userSolution))
            {
                userSolution = new DuelTaskSolution
                {
                    Solution = command.Solution,
                    Language = command.Language,
                };
            }
            else
            {
                userSolution.Solution = command.Solution;
                userSolution.Language = command.Language;
            }

            var updated = new Dictionary<char, DuelTaskSolution>(duel.User1Solutions)
            {
                [command.TaskKey] = userSolution
            };
            duel.User1Solutions = updated;
            opponentId = duel.User2.Id;
        }
        else if (duel.User2.Id == command.UserId)
        {
            if (!duel.User2Solutions.TryGetValue(command.TaskKey, out var userSolution))
            {
                userSolution = new DuelTaskSolution
                {
                    Solution = command.Solution,
                    Language = command.Language,
                };
            }
            else
            {
                userSolution.Solution = command.Solution;
                userSolution.Language = command.Language;
            }

            var updated = new Dictionary<char, DuelTaskSolution>(duel.User2Solutions)
            {
                [command.TaskKey] = userSolution
            };
            duel.User2Solutions = updated;
            opponentId = duel.User1.Id;
        }
        else
        {
            return new ForbiddenError(nameof(User), "update solution", nameof(User.Id), command.UserId);
        }

        if (duel.Configuration.ShouldShowOpponentSolution)
        {
            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = opponentId,
                    Message = new OpponentSolutionUpdatedMessage
                    {
                        DuelId = duel.Id,
                        TaskKey = command.TaskKey.ToString(),
                        Solution = command.Solution,
                        Language = command.Language
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

public sealed class UpdateDuelTaskSolutionCommandValidator : AbstractValidator<UpdateDuelTaskSolutionCommand>
{
    public UpdateDuelTaskSolutionCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.DuelId).GreaterThan(0);
        RuleFor(x => x.TaskKey).NotEmpty();
        RuleFor(x => x.Solution).NotEmpty();
        RuleFor(x => x.Language).IsInEnum();
    }
}
