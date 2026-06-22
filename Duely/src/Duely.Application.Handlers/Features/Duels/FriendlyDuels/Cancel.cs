// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Duels.FriendlyDuels;
//
// public sealed class CancelFriendlyDuelCommand : IRequest<Result>
// {
//     public required Guid Id { get; init; }
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class CancelFriendlyDuelHandler(Context context, ILogger<CancelFriendlyDuelHandler> logger)
//     : IRequestHandler<CancelFriendlyDuelCommand, Result>
// {
//     public async Task<Result> Handle(CancelFriendlyDuelCommand command, CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Where(u => u.Id == command.UserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//         
//         var duel = await context.Duels.OfType<FriendlyDuel>()
//             .Include(d => d.Participants)
//             .Where(d => d.Id == command.Id)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (duel is null)
//         {
//             return new DuelNotFoundError();
//         }
//
//         if (duel.CreatedBy.Id != user.Id)
//         {
//             return new ForbiddenError("Отменить дружескую дуэль может только создавший её пользователь.");
//         }
//         
//         if (duel.IsConfirmed)
//         {
//             return new ForbiddenError("Нельзя отменить подтверждённую дружескую дуэль.");
//         }
//
//         if (duel.Status != DuelStatus.Pending)
//         {
//             return new ForbiddenError("Нельзя отменить начатую дружескую дуэль.");
//         }
//         
//         var otherUser = duel.Participants.Single(u => u.Id != user.Id);
//
//         duel.Cancel();
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} canceled friendly duel with user {OtherNickname}",
//             user.Nickname, otherUser.Nickname);
//
//         return Result.Ok();
//     }
// }
