// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.UseCases.Features.Duels.FriendlyDuels;
//
// public sealed class ConfirmFriendlyDuelCommand : IRequest<Result>
// {
//     public required Guid Id { get; init; }
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class ConfirmFriendlyDuelHandler(Context context, ILogger<ConfirmFriendlyDuelHandler> logger)
//     : IRequestHandler<ConfirmFriendlyDuelCommand, Result>
// {
//     public async Task<Result> Handle(ConfirmFriendlyDuelCommand command, CancellationToken cancellationToken)
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
//             .Include(d => d.CreatedBy)
//             .Where(d => d.Id == command.Id)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (duel is null)
//         {
//             return new DuelNotFoundError();
//         }
//         
//         if (duel.Participants.All(p => p.Id != user.Id))
//         {
//             return new ForbiddenError();
//         }
//
//         if (user.Id == duel.CreatedBy.Id)
//         {
//             return new ForbiddenError("Дружескую дуэль может подтвердить только другой пользователь.");
//         }
//         
//         if (duel.IsConfirmed)
//         {
//             return new ForbiddenError("Нельзя заново потдвердить участие в дружеской дуэли.");
//         }
//         
//         var otherUser = duel.Participants.Single(u => u.Id != user.Id);
//
//         duel.Confirm(DateTime.UtcNow);
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} confirmed friendly duel with user {OtherNickname}",
//             user.Nickname, otherUser.Nickname);
//
//         return Result.Ok();
//     }
// }
