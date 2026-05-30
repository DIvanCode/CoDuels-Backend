using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class DeleteGroupMembershipCommand : IRequest<Result>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
    public required Guid TargetUserId { get; init; }
}

public sealed class DeleteGroupMembershipHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ILogger<DeleteGroupMembershipHandler> logger)
    : IRequestHandler<DeleteGroupMembershipCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupMembershipCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .Include(g => g.Name)
            .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }
        
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }

        var membership = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return new ForbiddenError();
        }
        
        var targetUser = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return new UserNotFoundError();
        }

        var targetMembership = await context.GroupMemberships
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.TargetUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (targetMembership is null)
        {
            return new ForbiddenError();
        }

        if (user.Id != targetUser.Id && !groupPermissionsService.CanDeleteMembership(membership, targetMembership))
        {
            return new ForbiddenError();
        }

        targetMembership.Delete();
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} deleted membership of user {TargetNickname} in group {GroupId}",
            user.Nickname, targetUser.Nickname, group.Id);

        return Result.Ok();
    }
}
