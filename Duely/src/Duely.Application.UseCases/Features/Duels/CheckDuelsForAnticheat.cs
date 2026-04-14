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

        foreach (var pendingScore in pendingScores)
        {
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
                    Actions = actions
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
