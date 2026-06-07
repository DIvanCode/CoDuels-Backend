using Duely.Application.UseCases.Dto.Groups;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class CreateGroupMembershipCommand : IRequest<Result<GroupMembershipShortDto>>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
    public required Guid TargetUserId { get; init; }
    public required GroupRole TargetUserRole { get; init; }
}

internal sealed class CreateGroupMembershipHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ILogger<CreateGroupMembershipHandler> logger)
    : IRequestHandler<CreateGroupMembershipCommand, Result<GroupMembershipShortDto>>
{
    public async Task<Result<GroupMembershipShortDto>> Handle(
        CreateGroupMembershipCommand command,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(g => g.Name)
            .Include(g => g.Memberships
                .Where(m => m.User.Id == command.UserId || m.User.Id == command.TargetUserId))
            .ThenInclude(u => u.User)
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

        var membership = group.Memberships.SingleOrDefault(m => m.User.Id == command.UserId);
        if (membership is null || !groupPermissionsService.CanCreateMembership(membership, command.TargetUserRole))
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
        
        var targetMembershipExists = group.Memberships.Any(m => m.User.Id == command.TargetUserId);
        if (targetMembershipExists)
        {
            return new EntityAlreadyExistsError("Пользователь уже состоит в группе.");
        }

        var targetMembership = group.CreateMembership(targetUser, command.TargetUserRole, isConfirmed: false);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} created membership of user {TargetNickname} in group {GroupId}",
            user.Nickname, targetUser.Nickname, group.Id);

        return new GroupMembershipShortDto
        {
            Role = targetMembership.Role,
            IsConfirmed = targetMembership.IsConfirmed
        };
    }
}
