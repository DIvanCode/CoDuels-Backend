using Duely.Application.UseCases.Dto.Groups;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class ConfirmGroupMembershipCommand : IRequest<Result<GroupMembershipShortDto>>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
}

public sealed class ConfirmGroupMembershipHandler(Context context, ILogger<ConfirmGroupMembershipHandler> logger)
    : IRequestHandler<ConfirmGroupMembershipCommand, Result<GroupMembershipShortDto>>
{
    public async Task<Result<GroupMembershipShortDto>> Handle(ConfirmGroupMembershipCommand command, CancellationToken cancellationToken)
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
            .Where(m => m.Group.Id == group.Id && m.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return new ForbiddenError();
        }

        membership.Confirm();
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} confirmed membership in group {GroupId}", user.Nickname, group.Id);

        return new GroupMembershipShortDto
        {
            Role = membership.Role,
            IsConfirmed = membership.IsConfirmed
        };
    }
}
