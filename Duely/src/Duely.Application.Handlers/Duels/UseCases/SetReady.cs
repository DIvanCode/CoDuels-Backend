using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.UseCases;

public sealed class SetReadyToDuelCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

internal sealed class SetReadyToDuelHandler(
    Context context,
    ILogger<SetReadyToDuelHandler> logger)
    : IRequestHandler<SetReadyToDuelCommand, Result>
{
    public async Task<Result> Handle(SetReadyToDuelCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }

        var duel = await context.Duels
            .Where(d => d.Id == command.DuelId)
            .Include(d => d.Participants)
            .ThenInclude(p => p.User)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }

        if (duel.Participants.All(p => p.User.Id != user.Id))
        {
            return new ForbiddenError("Дуэль может принять только участник дуэли.");
        }
        
        var participant = duel.Participants.Single(p => p.User.Id == user.Id);
        if (participant.IsReady)
        {
            return Result.Ok();
        }
        
        participant.SetReady();
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} is ready for duel {DuelId}", user.Nickname, duel.Id);

        return Result.Ok();
    }
}
