using Duely.Application.UseCases.Dto.Duels;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.UseCases.Features.Duels.GroupDuels;

public sealed class CreateGroupDuelCommand : IRequest<Result<GroupDuelDto>>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
    public required IReadOnlyCollection<Guid> Participants { get; init; }
    public required Guid? DuelConfigurationId { get; init; }
}

internal sealed class CreateGroupDuelHandler(
    Context context,
    IOptions<DuelOptions> options,
    IGroupPermissionsService groupPermissionsService,
    ILogger<CreateGroupDuelHandler> logger)
    : IRequestHandler<CreateGroupDuelCommand, Result<GroupDuelDto>>
{
    public async Task<Result<GroupDuelDto>> Handle(CreateGroupDuelCommand command, CancellationToken cancellationToken)
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
            .Where(m => m.User.Id == command.UserId && m.Group.Id == command.GroupId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return new ForbiddenError();
        }

        if (!groupPermissionsService.CanCreateDuel(membership))
        {
            return new ForbiddenError("У вас недостаточно прав для создания дуэли в этой группе.");
        }
        
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
        
        var duelConfigurationResult = await ResolveDuelConfiguration(command.DuelConfigurationId, cancellationToken);
        if (duelConfigurationResult.IsFailed)
        {
            return duelConfigurationResult.ToResult();
        }
        
        var duelConfiguration = duelConfigurationResult.Value;
        var id = new DuelId(Guid.NewGuid());
        var duel = new GroupDuel(id, duelConfiguration, participants, DateTime.UtcNow, group, user);
        
        context.Duels.Add(duel);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {Nickname} created duel in group {Group} with users {Participants}",
            user.Nickname, group.Id, string.Join(", ", participants.Select(p => p.Nickname)));

        return new GroupDuelDto
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
            CreatedBy = new UserShortDto
            {
                Id = duel.CreatedBy.Id,
                Nickname = duel.CreatedBy.Nickname.Value
            },
            IsConfirmed = duel.IsConfirmed
                .ToDictionary(x => x.Key.Value, x => x.Value)
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
            duelConfiguration?.ShouldShowOpponentSolution ?? options.Value.DefaultShouldShowOpponentSolution,
            duelConfiguration?.DurationMinutes ?? options.Value.DefaultDurationMinutes,
            duelConfiguration?.ProblemsCount ?? options.Value.DefaultProblemsCount,
            duelConfiguration?.ProblemsOrder ?? options.Value.DefaultProblemsOrder);

        context.DuelConfigurations.Add(duelConfigurationVersion);
        
        return duelConfigurationVersion;
    }
}

internal sealed class CreateGroupDuelCommandValidator : AbstractValidator<CreateGroupDuelCommand>
{
    public CreateGroupDuelCommandValidator()
    {
        RuleFor(x => x.Participants)
            .Must(x => x.Distinct().Count() == 2)
            .WithMessage("Дуэль в группе должна быть между двумя разными пользователями.");
    }
}
