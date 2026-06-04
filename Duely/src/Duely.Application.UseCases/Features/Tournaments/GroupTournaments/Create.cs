using Duely.Application.UseCases.Dto.Tournaments;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Users.Entities;
using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments.GroupTournaments;

public sealed class CreateGroupTournamentCommand : IRequest<Result<GroupTournamentDto>>
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Guid> Participants { get; init; }
    public required TournamentConfigurationType ConfigurationType { get; init; }
    public required Guid? DuelConfigurationId { get; init; }
    public required Guid GroupId { get; init; }
}

public sealed class CreateGroupTournamentHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<CreateGroupTournamentCommand, Result<GroupTournamentDto>>
{
    public async Task<Result<GroupTournamentDto>> Handle(
        CreateGroupTournamentCommand command,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var group = await context.Groups
            .AsNoTracking()
            .Include(g => g.Name)
            .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }
        
        var membership = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == command.GroupId && m.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || !groupPermissionsService.CanCreateTournament(membership))
        {
            return new ForbiddenError();
        }

        DuelConfiguration? configuration = null;
        if (command.DuelConfigurationId is not null)
        {
            configuration = await context.DuelConfigurations
                .AsNoTracking()
                .Where(c => c.Id == command.DuelConfigurationId.Value)
                .SingleOrDefaultAsync(cancellationToken);
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
