using Duely.Application.UseCases.Dto.Tournaments;
using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Errors;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.UseCases.Features.Tournaments.GlobalTournaments;

public sealed class CreateGlobalTournamentCommand : IRequest<Result<GlobalTournamentDto>>
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Guid> Participants { get; init; }
    public required TournamentConfigurationType ConfigurationType { get; init; }
    public required Guid? DuelConfigurationId { get; init; }
}

public sealed class CreateGlobalTournamentHandler(
    Context context,
    IOptions<DuelOptions> duelOptions,
    ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory,
    ILogger<CreateGlobalTournamentHandler> logger)
    : IRequestHandler<CreateGlobalTournamentCommand, Result<GlobalTournamentDto>>
{
    public async Task<Result<GlobalTournamentDto>> Handle(
        CreateGlobalTournamentCommand command,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null || !user.IsAdmin)
        {
            return new ForbiddenError();
        }

        var duelConfigurationResult = await ResolveDuelConfiguration(command.DuelConfigurationId, cancellationToken);
        if (duelConfigurationResult.IsFailed)
        {
            return duelConfigurationResult.ToResult();
        }
        
        var duelConfiguration = duelConfigurationResult.Value;
        var configuration = TournamentConfiguration.Create(command.ConfigurationType, duelConfiguration);
        var id = new TournamentId(Guid.NewGuid());
        var name = new TournamentName(command.Name);
        var tournament = new GlobalTournament(id, name, user, DateTime.UtcNow, configuration);

        foreach (var participantId in command.Participants)
        {
            var participant = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == participantId, cancellationToken);
            if (participant is null)
            {
                return new UserNotFoundError();
            }

            tournament.AddParticipant(participant);
        }

        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} created global tournament {TournamentId}",
            user.Nickname, tournament.Id);

        return new GlobalTournamentDto
        {
            Id = tournament.Id,
            Name = tournament.Name.Value,
            Type = tournament.Type,
            Status = tournament.Status,
            CreatedBy = new UserShortDto
            {
                Id = tournament.CreatedBy.Id,
                Nickname = tournament.CreatedBy.Nickname.Value
            },
            CreatedAt = tournament.CreatedAt,
            Participants = tournament.Participants
                .Select(p => new UserShortDto
                {
                    Id = p.User.Id,
                    Nickname = p.User.Nickname.Value
                })
                .ToList(),
            Configuration = tournamentConfigurationDtoFactory.Create(tournament.Configuration)
        };
    }
    
    private async Task<Result<DuelConfiguration>> ResolveDuelConfiguration(
        Guid? duelConfigurationId,
        CancellationToken cancellationToken)
    {
        DuelConfiguration? duelConfiguration = null;
        if (duelConfigurationId is not null)
        {
            duelConfiguration = await context.DuelConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == duelConfigurationId, cancellationToken);
            if (duelConfiguration is null)
            {
                return new DuelConfigurationNotFoundError();
            }
        }
        
        var duelConfigurationVersionId = new DuelConfigurationId(Guid.NewGuid());
        var duelConfigurationVersion = new DuelConfiguration(
            duelConfigurationVersionId,
            duelConfiguration?.IsRated ?? false,
            duelConfiguration?.ShouldShowOpponentSolution ?? duelOptions.Value.DefaultShouldShowOpponentSolution,
            duelConfiguration?.DurationMinutes ?? duelOptions.Value.DefaultDurationMinutes,
            duelConfiguration?.ProblemsCount ?? duelOptions.Value.DefaultProblemsCount,
            duelConfiguration?.ProblemsOrder ?? duelOptions.Value.DefaultProblemsOrder);

        context.DuelConfigurations.Add(duelConfigurationVersion);
        
        return duelConfigurationVersion;
    }
}

public sealed class CreateGlobalTournamentCommandValidator : AbstractValidator<CreateGlobalTournamentCommand>
{
    public CreateGlobalTournamentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название турнира не может быть пустым.");
        
        RuleFor(x => x.Name)
            .MaximumLength(TournamentName.MaxLength)
            .WithMessage($"Название турнира не может содержать более {TournamentName.MaxLength} символов.");

        RuleFor(r => r.Participants)
            .Must(p => p.Distinct().Count() >= 2)
            .WithMessage("Турнир должен содержать хотя бы двух различных участников.");
    }
}
