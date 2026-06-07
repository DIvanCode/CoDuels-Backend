using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.GroupDuels;

public sealed class DeleteGroupDuelCommand : IRequest<Result>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
}

internal sealed class DeleteGroupDuelHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ILogger<DeleteGroupDuelHandler> logger)
    : IRequestHandler<DeleteGroupDuelCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupDuelCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var duel = await context.Duels.OfType<GroupDuel>()
            .Include(d => d.Participants)
            .ThenInclude(p => p.Nickname)
            .Include(d => d.Group)
            .SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }
        
        var group = await context.Groups
            .AsNoTracking()
            .Include(g => g.Name)
            .Include(g => g.Memberships.Where(m => m.User.Id == command.UserId))
            .ThenInclude(m => m.User)
            .SingleOrDefaultAsync(g => g.Id == duel.Group.Id, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }

        var membership = group.GetMembership(user);
        if (membership is null)
        {
            return new ForbiddenError();            
        }
        
        if (!groupPermissionsService.CanDeleteDuel(membership))
        {
            return new ForbiddenError("У вас недостаточно прав для удаления дуэли в этой группе.");
        }
        
        if (duel.Status != DuelStatus.Pending)
        {
            return new ForbiddenError("Нельзя отменить начатую дуэль в группе.");
        }

        duel.Delete();
        
        context.Duels.Remove(duel);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} deleted duel in group {Group} with users {Participants}",
            user.Nickname, duel.Group.Id, string.Join(", ", duel.Participants.Select(p => p.Nickname)));

        return Result.Ok();
    }
}
