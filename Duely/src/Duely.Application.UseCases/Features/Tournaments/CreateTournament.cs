using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class CreateTournamentCommand : IRequest<Result<TournamentDto>>
{
    public required int UserId { get; init; }
    public required string Name { get; init; }
    public required int GroupId { get; init; }
    public required TournamentMatchmakingType MatchmakingType { get; init; }
    public required List<string> Participants { get; init; }
    public int? DuelConfigurationId { get; init; }
}

public sealed class CreateTournamentHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ITournamentMatchmakingStrategyResolver strategyResolver)
    : IRequestHandler<CreateTournamentCommand, Result<TournamentDto>>
{
    private const string Operation = "create tournament in";

    public async Task<Result<TournamentDto>> Handle(CreateTournamentCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }

        var actorMembership = group.Users.SingleOrDefault(m => m.User.Id == command.UserId);
        if (actorMembership is null || !groupPermissionsService.CanCreateTournament(actorMembership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }

        DuelConfiguration? configuration = null;
        if (command.DuelConfigurationId is not null)
        {
            configuration = await context.DuelConfigurations
                .SingleOrDefaultAsync(c => c.Id == command.DuelConfigurationId.Value, cancellationToken);
            if (configuration is null)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.DuelConfigurationId.Value);
            }
        }

        var participantUsers = new List<User>();
        foreach (var nickname in command.Participants)
        {
            var membership = group.Users
                .SingleOrDefault(m => !m.InvitationPending && m.User.Nickname == nickname);
            if (membership is null)
            {
                return new EntityNotFoundError(nameof(GroupMembership), nameof(User.Nickname), nickname);
            }

            participantUsers.Add(membership.User);
        }

        var strategy = strategyResolver.GetStrategy(command.MatchmakingType);
        var tournament = strategy.CreateTournament(
            command.Name,
            group,
            actorMembership.User,
            DateTime.UtcNow,
            configuration,
            participantUsers);

        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync(cancellationToken);

        return TournamentDtoMapper.Map(tournament);
    }
}

public sealed class CreateTournamentCommandValidator : AbstractValidator<CreateTournamentCommand>
{
    public CreateTournamentCommandValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("tournament name is required");

        RuleFor(r => r.MatchmakingType)
            .IsInEnum().WithMessage("matchmaking type has invalid value");

        RuleFor(r => r.Participants)
            .Must(p => p.Count >= 2).WithMessage("at least two participants are required")
            .Must(p => p.Distinct(StringComparer.Ordinal).Count() == p.Count)
            .WithMessage("participants must be unique");

        RuleForEach(r => r.Participants)
            .NotEmpty().WithMessage("participant nickname is required");
    }
}
