using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class InviteUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required int InvitedUserId { get; init; }
    public required GroupRole Role { get; init; }
}

public sealed class InviteUserHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<InviteUserCommand, Result>
{
    private const string Operation = "invite user to";
    
    public async Task<Result> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }

        var membership = await context.GroupMemberships
            .Include(m => m.User)
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null ||
            !groupPermissionsService.CanAssignRole(membership, command.Role) ||
            command.UserId == command.InvitedUserId)
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }

        var invitedUser = await context.Users
            .SingleOrDefaultAsync(u => u.Id == command.InvitedUserId, cancellationToken);
        if (invitedUser is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.InvitedUserId);
        }

        var invitedMembership = await context.GroupMemberships.SingleOrDefaultAsync(
            m => m.Group.Id == group.Id && m.User.Id == command.InvitedUserId,
            cancellationToken);
        if (invitedMembership is not null)
        {
            return new EntityAlreadyExistsError(nameof(User), nameof(Group.Id), command.GroupId);
        }
        
        invitedMembership = new GroupMembership
        {
            User = invitedUser,
            Group = group,
            Role = command.Role,
            InvitationPending = true,
            InvitedBy = membership.User
        };

        context.GroupMemberships.Add(invitedMembership);
        
        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = invitedUser.Id,
                Message = new GroupInvitationMessage
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Role = command.Role,
                    InvitedBy = membership.User.Nickname
                }
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });
        
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
