using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed record CheckDuelsForAnticheatCommand(bool ShouldCleanupUserActions) : IRequest<Result>;

public sealed class CheckDuelsForAnticheatHandler(Context context, IAnalyzerClient analyzerClient)
    : IRequestHandler<CheckDuelsForAnticheatCommand, Result>
{
    private const int PendingScoresBatchSize = 20;

    public async Task<Result> Handle(CheckDuelsForAnticheatCommand request, CancellationToken cancellationToken)
    {
        var pendingScores = await context.AnticheatScores
            .Include(score => score.Duel)
            .ThenInclude(duel => duel.User1)
            .Include(score => score.Duel)
            .ThenInclude(duel => duel.User2)
            .Include(score => score.User)
            .Where(score => !score.Score.HasValue)
            .OrderBy(score => score.Duel.Id)
            .ThenBy(score => score.User.Id)
            .ThenBy(score => score.TaskKey)
            .Take(PendingScoresBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var pendingScore in pendingScores)
        {
            var duel = pendingScore.Duel;
            var user = pendingScore.User;

            var userRating = user.Id switch
            {
                var id when id == duel.User1.Id => duel.User1InitRating,
                var id when id == duel.User2.Id => duel.User2InitRating,
                _ => -1
            };
            if (userRating < 0)
            {
                return new EntityNotFoundError(
                    nameof(User),
                    $"Id = {user.Id} in '{nameof(Duel)}' with '{nameof(Duel.Id)}' = '{duel.Id}'");
            }

            var actions = await context.UserActions
                .Where(action =>
                    action.DuelId == duel.Id &&
                    action.UserId == user.Id &&
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
            if (request.ShouldCleanupUserActions)
            {
                context.UserActions.RemoveRange(actions);
            }
        }
        
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
