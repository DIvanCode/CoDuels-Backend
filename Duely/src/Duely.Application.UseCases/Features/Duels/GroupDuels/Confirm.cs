using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.GroupDuels;

public sealed class ConfirmGroupDuelCommand : IRequest<Result>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
}

internal sealed class ConfirmGroupDuelHandler(
    Context context,
    ILogger<ConfirmGroupDuelHandler> logger)
    : IRequestHandler<ConfirmGroupDuelCommand, Result>
{
    public async Task<Result> Handle(ConfirmGroupDuelCommand command, CancellationToken cancellationToken)
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
        
        if (duel.Participants.All(p => p.Id != user.Id))
        {
            return new ForbiddenError();
        }
        
        var otherUser = duel.Participants.Single(u => u.Id != user.Id);

        duel.Confirm(DateTime.UtcNow, user.Id);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} confirmed duel in group {Group} with user {OtherNickname}",
            user.Nickname, duel.Group.Id, otherUser.Nickname);

        return Result.Ok();
    }
}
