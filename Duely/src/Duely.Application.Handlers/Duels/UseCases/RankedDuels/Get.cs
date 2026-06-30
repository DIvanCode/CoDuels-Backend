using Duely.Application.Handlers.Duels.Models;
using Duely.Application.Handlers.Duels.Models.DuelParticipants;
using Duely.Application.Handlers.Users.Models;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Entities.DuelParticipants;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Handlers.Duels.UseCases.RankedDuels;

public sealed class GetRankedDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

internal sealed class GetRankedDuelHandler(Context context) : IRequestHandler<GetRankedDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetRankedDuelQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == query.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }

        var duel = await context.Duels.OfType<RankedDuel>()
            .Where(d => d.Id == query.DuelId)
            .Include(d => d.Configuration)
            .Include(d => d.Participants)
            .ThenInclude(p => p.User)
            .Include(d => d.Problems)
            .ThenInclude(p => p.Problem)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }

        if (duel.Participants.All(p => p.User.Id != query.UserId))
        {
            return new ForbiddenError();
        }

        return new DuelDto
        {
            Id = duel.Id,
            Type = duel.Type,
            Configuration = new DuelConfigurationDto
            {
                Id = duel.Configuration.Id,
                IsRated = duel.Configuration.IsRated,
                ShowOpponentSolution = duel.Configuration.ShowOpponentSolution,
                DurationMinutes = duel.Configuration.DurationMinutes,
                ProblemsCount = duel.Configuration.ProblemsCount,
                ProblemsOrder = duel.Configuration.ProblemsOrder
            },
            Participants = duel.Participants
                .Select(participant =>
                {
                    var p = (RankedDuelParticipant) participant;
                    return new RankedDuelParticipantDto
                    {
                        User = new UserShortDto
                        {
                            Id = p.User.Id,
                            Nickname = p.User.Nickname
                        },
                        IsReady = p.IsReady,
                        InitialRating = p.InitialRating
                    };
                })
                .ToList(),
            Problems = duel.Problems
                .Select(p => new DuelProblemDto
                {
                    Problem = new ProblemDto
                    {
                        InternalId = p.Problem.Id,
                        SystemName = p.Problem.ExternalSystemName,
                        Id = p.Problem.ExternalId,
                        Title = p.Problem.Title
                    },
                    Position = p.Position
                })
                .ToList(),
            Status = duel.Status,
            CreatedAt = duel.CreatedAt,
            StartedAt = duel.StartedAt
        };
    }
}
