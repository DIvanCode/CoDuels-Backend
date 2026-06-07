using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class DeclineGroupMembershipCommand : IRequest<Result>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
}

internal sealed class DeclineGroupMembershipHandler(Context context, ILogger<DeclineGroupMembershipHandler> logger)
    : IRequestHandler<DeclineGroupMembershipCommand, Result>
{
    public async Task<Result> Handle(DeclineGroupMembershipCommand command, CancellationToken cancellationToken)
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
            .Include(g => g.Memberships.Where(m => m.User.Id == command.UserId))
            .ThenInclude(m => m.User)
            .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }

        var membership = group.GetMembership(user);
        if (membership is null)
        {
            return new ForbiddenError();
        }

        if (membership.IsConfirmed)
        {
            return new ForbiddenError("Нельзя отлонить ранее принятое приглашение в группу.");
        }

        group.DeclineMembership(user);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} declined membership in group {GroupId}", user.Nickname, group.Id);

        return Result.Ok();
    }
}
