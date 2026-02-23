using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class CancelInviteCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required int InvitedUserId { get; init; }
}

public sealed class CancelInviteHandler(Context context) : IRequestHandler<CancelInviteCommand, Result>
{
    private const string Operation = "cancel invite in";
    
    public async Task<Result> Handle(CancelInviteCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }

        var membership = await context.GroupMemberships
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.InvitedUserId && m.InvitationPending)
            .Include(m => m.User)
            .Include(m => m.InvitedBy)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || membership.InvitedBy?.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }

        context.GroupMemberships.Remove(membership);
        
        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = membership.User.Id,
                Message = new GroupInvitationCanceledMessage
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    InvitedBy = membership.InvitedBy!.Nickname
                }
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });
        
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
