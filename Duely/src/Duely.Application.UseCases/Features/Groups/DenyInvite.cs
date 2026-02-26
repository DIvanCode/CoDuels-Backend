using Duely.Application.Services.Errors;
using Duely.Domain.Models.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class DenyInviteCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
}

public sealed class DenyInviteHandler(Context context) : IRequestHandler<DenyInviteCommand, Result>
{
    private const string Operation = "deny invite in";
    
    public async Task<Result> Handle(DenyInviteCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }

        var membership = await context.GroupMemberships
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.UserId && m.InvitationPending)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }

        context.GroupMemberships.Remove(membership);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
