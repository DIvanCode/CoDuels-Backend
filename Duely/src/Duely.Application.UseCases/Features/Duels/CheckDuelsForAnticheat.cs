using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForAnticheatCommand : IRequest<Result>;

public sealed class CheckDuelsForAnticheatHandler(Context context, IAnalyzerClient analyzerClient)
    : IRequestHandler<CheckDuelsForAnticheatCommand, Result>
{
    private const int PendingScoresBatchSize = 20;

    public async Task<Result> Handle(CheckDuelsForAnticheatCommand request, CancellationToken cancellationToken)
    {
        var pendingScores = await context.AnticheatScores
            .Where(score => !score.Score.HasValue)
            .OrderBy(score => score.DuelId)
            .ThenBy(score => score.UserId)
            .ThenBy(score => score.TaskKey)
            .Take(PendingScoresBatchSize)
            .ToListAsync(cancellationToken);

        var duelIds = pendingScores
            .Select(score => score.DuelId)
            .Distinct()
            .ToList();

        var duelsById = await context.Duels
            .Include(duel => duel.User1)
            .Include(duel => duel.User2)
            .Where(duel => duelIds.Contains(duel.Id))
            .ToDictionaryAsync(duel => duel.Id, cancellationToken);

        foreach (var pendingScore in pendingScores)
        {
            if (!duelsById.TryGetValue(pendingScore.DuelId, out var duel))
            {
                return Result.Fail($"duel {pendingScore.DuelId} not found");
            }

            var userRating = pendingScore.UserId switch
            {
                var id when id == duel.User1.Id => duel.User1InitRating,
                var id when id == duel.User2.Id => duel.User2InitRating,
                _ => -1
            };
            if (userRating < 0)
            {
                return Result.Fail($"user {pendingScore.UserId} is not a duel participant for duel {pendingScore.DuelId}");
            }

            var actions = await context.UserActions
                .Where(action =>
                    action.DuelId == pendingScore.DuelId &&
                    action.UserId == pendingScore.UserId &&
                    action.TaskKey == pendingScore.TaskKey)
                .OrderBy(action => action.SequenceId)
                .ToListAsync(cancellationToken);

            var predictResult = await analyzerClient.PredictAsync(
                new PredictRequest
                {
                    Actions = actions,
                    UserRating = userRating
                },
                cancellationToken);

            if (predictResult.IsFailed)
            {
                return predictResult.ToResult();
            }

            pendingScore.Score = predictResult.Value.Score;
            context.UserActions.RemoveRange(actions);
        }
        
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
