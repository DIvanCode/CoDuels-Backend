using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.FriendlyDuels;

public sealed class DeleteFriendlyDuelCommand : IRequest<Result>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
}

internal sealed class DeleteFriendlyDuelHandler(
    Context context,
    ILogger<DeleteFriendlyDuelHandler> logger)
    : IRequestHandler<DeleteFriendlyDuelCommand, Result>
{
    public async Task<Result> Handle(DeleteFriendlyDuelCommand command, CancellationToken cancellationToken)
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
            .SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }

        if (duel.CreatedBy.Id != user.Id)
        {
            return new ForbiddenError("Удалить дружескую дуэль может только создавший её пользователь.");
        }
        
        if (duel.IsConfirmed)
        {
            return new ForbiddenError("Нельзя отменить подтверждённую дружескую дуэль.");
        }

        if (duel.Status != DuelStatus.Pending)
        {
            return new ForbiddenError("Нельзя отменить начатую дружескую дуэль.");
        }
        
        var otherUser = duel.Participants.Single(u => u.Id != user.Id);

        duel.Delete();
        
        context.Duels.Remove(duel);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} deleted friendly duel with user {OtherNickname}",
            user.Nickname, otherUser.Nickname);

        return Result.Ok();
    }
}
