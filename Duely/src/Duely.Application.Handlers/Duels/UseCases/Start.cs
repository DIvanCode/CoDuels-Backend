using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.UseCases;

public sealed class StartDuelCommand : IRequest<Result>
{
    public required int DuelId { get; init; }
}

internal sealed class StartDuelHandler(
    Context context,
    ILogger<StartDuelHandler> logger)
    : IRequestHandler<StartDuelCommand, Result>
{
    public async Task<Result> Handle(StartDuelCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Id == command.DuelId)
            .Include(d => d.Configuration)
            .Include(d => d.Participants)
            .ThenInclude(p => p.User)
            .ThenInclude(u => u.DuelsParticipation)
            .ThenInclude(dp => dp.Duel)
            .ThenInclude(d => d.Problems)
            .ThenInclude(p => p.Problem)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }

        if (duel.Status is not DuelStatus.Ready)
        {
            return new InvalidOperationError("Нельзя начать уже начатую дуэль.");
        }

        if (duel.Participants.Any(p => !p.IsReady))
        {
            return new InvalidOperationError("Для начала дуэли все участники должны быть готовы.");
        }

        var seenProblems = duel.Participants
            .SelectMany(participant => participant.User.DuelsParticipation
                .SelectMany(participation => participation.Duel.Problems
                    .Select(duelProblem => duelProblem.Problem.Id)));
        var problems = await context.Problems
            .Where(p => !seenProblems.Contains(p.Id))
            .Take(duel.Configuration.ProblemsCount)
            .ToListAsync(cancellationToken);
        
        var isFirst = true;
        foreach (var problem in problems)
        {
            var isVisible = duel.Configuration.ProblemsOrder switch
            {
                ProblemsOrder.Sequential => isFirst,
                _ => true
            };
            
            duel.AddProblem(problem, isVisible);
            isFirst = false;
        }

        duel.Start();
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Started {DuelType} duel {DuelId} with users {Participants}",
            duel.Id, duel.Type, string.Join(", ", duel.Participants.Select(p => p.User.Nickname)));

        return Result.Ok();
    }
}
