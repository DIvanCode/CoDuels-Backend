using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class ExcludeUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required int TargetUserId { get; init; }
}

public sealed class ExcludeUserHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<ExcludeUserCommand, Result>
{
    private const string Operation = "exclude user from";
    
    public async Task<Result> Handle(ExcludeUserCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), command.GroupId);
        }

        var targetMembership = await context.GroupMemberships
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.TargetUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (targetMembership is null)
        {
            return new EntityNotFoundError(nameof(GroupMembership), nameof(User.Id), command.TargetUserId);
        }

        var membership = await context.GroupMemberships
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || !groupPermissionsService.CanExclude(membership, targetMembership.Role))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), command.GroupId);
        }

        context.GroupMemberships.Remove(targetMembership);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
