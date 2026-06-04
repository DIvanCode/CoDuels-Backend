using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.FriendlyDuels;

public sealed class ConfirmFriendlyDuelCommand : IRequest<Result>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
}

internal sealed class ConfirmFriendlyDuelHandler(
    Context context,
    ILogger<ConfirmFriendlyDuelHandler> logger)
    : IRequestHandler<ConfirmFriendlyDuelCommand, Result>
{
    public async Task<Result> Handle(ConfirmFriendlyDuelCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var duel = await context.Duels.OfType<FriendlyDuel>()
            .Include(d => d.Participants)
                .ThenInclude(p => p.Nickname)
            .Include(d => d.CreatedBy)
            .SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }
        
        if (duel.Participants.All(p => p.Id != user.Id))
        {
            return new ForbiddenError();
        }

        if (user.Id == duel.CreatedBy.Id)
        {
            return new ForbiddenError("Дружескую дуэль может подтвердить только другой пользователь.");
        }
        
        if (duel.IsConfirmed)
        {
            return new ForbiddenError("Нельзя заново потдвердить участие в дружеской дуэли.");
        }
        
        var otherUser = duel.Participants.Single(u => u.Id != user.Id);

        duel.Confirm(DateTime.UtcNow);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} confirmed friendly duel with user {OtherNickname}",
            user.Nickname, otherUser.Nickname);

        return Result.Ok();
    }
}
