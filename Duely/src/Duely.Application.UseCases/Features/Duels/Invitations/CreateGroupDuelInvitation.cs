using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class CreateGroupDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required int User1Id { get; init; }
    public required int User2Id { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class CreateGroupDuelInvitationHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<CreateGroupDuelInvitationCommand, Result>
{
    private const string Operation = "create duel in";
    
    public async Task<Result> Handle(CreateGroupDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user == null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        
        var group = await context.Groups
            .Include(g => g.Users.Where(m =>
                m.User.Id == command.UserId || m.User.Id == command.User1Id || m.User.Id == command.User2Id))
            .ThenInclude(m => m.User)
            .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }
        
        var membership = group.Users.SingleOrDefault(m => m.User.Id == user.Id);
        if (membership is null || !groupPermissionsService.CanCreateDuel(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }
        
        var user1 = await context.Users.SingleOrDefaultAsync(u => u.Id == command.User1Id, cancellationToken);
        if (user1 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.User1Id);
        }
        
        var user2 = await context.Users.SingleOrDefaultAsync(u => u.Id == command.User2Id, cancellationToken);
        if (user2 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.User2Id);
        }

        var membership1 = group.Users.SingleOrDefault(m => m.User.Id == user1.Id);
        if (membership1 is null)
        {
            return new EntityNotFoundError(nameof(GroupMembership), nameof(User.Id), user1.Id);
        }
        
        var membership2 = group.Users.SingleOrDefault(m => m.User.Id == user2.Id);
        if (membership2 is null)
        {
            return new EntityNotFoundError(nameof(GroupMembership), nameof(User.Id), user2.Id);
        }

        var user1ActiveDuel = await context.Duels
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                    d.Status == DuelStatus.InProgress &&
                    (d.User1.Id == user1.Id || d.User2.Id == user1.Id),
                cancellationToken);
        if (user1ActiveDuel is not null)
        {
            return new EntityAlreadyExistsError(nameof(Duel), nameof(User.Id), user1.Id);
        }
        
        var user2ActiveDuel = await context.Duels
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                    d.Status == DuelStatus.InProgress &&
                    (d.User1.Id == user2.Id || d.User2.Id == user2.Id),
                cancellationToken);
        if (user2ActiveDuel is not null)
        {
            return new EntityAlreadyExistsError(nameof(Duel), nameof(User.Id), user2.Id);
        }
        
        DuelConfiguration? configuration = null;
        if (command.ConfigurationId is not null)
        {
            configuration = await context.DuelConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == command.ConfigurationId, cancellationToken);
            if (configuration is null)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.ConfigurationId.Value);
            }
        }
        
        var groupPendingDuel = await context.PendingDuels.OfType<GroupPendingDuel>()
            .SingleOrDefaultAsync(d =>
                    d.Group.Id == command.GroupId &&
                    ((d.User1.Id == user1.Id && d.User2.Id == user2.Id) ||
                     (d.User1.Id == user2.Id && d.User2.Id == user1.Id)) &&
                    ((command.ConfigurationId == null && d.Configuration == null) ||
                     (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (groupPendingDuel is not null)
        {
            return Result.Ok();
        }

        groupPendingDuel = new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user,
            User1 = user1,
            User2 = user2,
            Configuration = configuration,
            CreatedAt = DateTime.UtcNow
        };
        context.PendingDuels.Add(groupPendingDuel);

        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = user1.Id,
                Message = new GroupDuelInvitationMessage
                {
                    GroupName = group.Name,
                    OpponentNickname = user2.Nickname,
                    ConfigurationId = command.ConfigurationId
                }
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });
        
        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = user2.Id,
                Message = new GroupDuelInvitationMessage
                {
                    GroupName = group.Name,
                    OpponentNickname = user1.Nickname,
                    ConfigurationId = command.ConfigurationId
                }
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
