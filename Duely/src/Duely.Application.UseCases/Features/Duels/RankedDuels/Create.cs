using Duely.Application.UseCases.Dto.Duels;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.UseCases.Features.Duels.RankedDuels;

public sealed class CreateRankedDuelCommand : IRequest<Result<RankedDuelDto>>
{
    public required IReadOnlyCollection<Guid> Participants { get; init; }
}

internal sealed class CreateRankedDuelHandler(
    Context context,
    IOptions<DuelOptions> options,
    ILogger<CreateRankedDuelHandler> logger)
    : IRequestHandler<CreateRankedDuelCommand, Result<RankedDuelDto>>
{
    public async Task<Result<RankedDuelDto>> Handle(
        CreateRankedDuelCommand command,
        CancellationToken cancellationToken)
    {
        var participants = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .Where(u => command.Participants.Contains(u.Id))
            .ToListAsync(cancellationToken);
        if (participants.Count != 2)
        {
            return new UserNotFoundError();
        }
        
        var id = new DuelId(Guid.NewGuid());
        var duelConfiguration = CreateDuelConfiguration();
        var initRatings = participants.ToDictionary(p => p.Id, p => p.Rating);
        var duel = new RankedDuel(id, duelConfiguration, participants, DateTime.UtcNow, initRatings);
        
        context.Duels.Add(duel);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Created ranked duel with users {Participants}",
            string.Join(", ", participants.Select(p => p.Nickname)));

        return new RankedDuelDto
        {
            Id = duel.Id,
            Type = duel.Type,
            Configuration = new DuelConfigurationDto
            {
                Id = duel.Configuration.Id,
                ShouldShowOpponentSolution = duel.Configuration.ShouldShowOpponentSolution,
                DurationMinutes = duel.Configuration.DurationMinutes,
                ProblemsCount = duel.Configuration.ProblemsCount,
                ProblemsOrder = duel.Configuration.ProblemsOrder
            },
            Participants = duel.Participants
                .Select(p => new UserShortDto
                {
                    Id = p.Id,
                    Nickname = p.Nickname.Value
                })
                .ToList(),
            ProblemSet = duel.ProblemSet is null
                ? null
                : new ProblemSetDto
                {
                    Problems = duel.ProblemSet.Problems
                        .Where(p => p.IsVisible)
                        .Select(p => new ProblemDto
                        {
                            Position = p.Position,
                            ExternalId = p.ExternalId
                        })
                        .ToList()
                },
            Status = duel.Status,
            CreatedAt = duel.CreatedAt,
            StartedAt = duel.StartedAt,
            FinishedAt = duel.FinishedAt,
            Winner = duel.Winner is null
                ? null
                : new UserShortDto
                {
                    Id = duel.Winner.Id,
                    Nickname = duel.Winner.Nickname.Value
                },
            InitRatings = duel.InitRatings
                .ToDictionary(x => x.Key.Value, x => x.Value.Value),
            FinalRatings = duel.FinalRatings?
                .ToDictionary(x => x.Key.Value, x => x.Value.Value)
        };
    }

    private DuelConfiguration CreateDuelConfiguration()
    {
        var duelConfigurationId = new DuelConfigurationId(Guid.NewGuid());
        var duelConfiguration = new DuelConfiguration(
            duelConfigurationId,
            isRated: true,
            options.Value.DefaultShouldShowOpponentSolution,
            options.Value.DefaultDurationMinutes,
            options.Value.DefaultProblemsCount,
            options.Value.DefaultProblemsOrder);

        context.DuelConfigurations.Add(duelConfiguration);
        
        return duelConfiguration;
    }
}

internal sealed class CreateRankedDuelCommandValidator : AbstractValidator<CreateRankedDuelCommand>
{
    public CreateRankedDuelCommandValidator()
    {
        RuleFor(x => x.Participants)
            .Must(x => x.Distinct().Count() == 2)
            .WithMessage("Рейтинговая дуэль проводится между двумя пользователями.");
    }
}
