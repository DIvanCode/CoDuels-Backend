// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Duels.GroupDuels;
//
// public sealed class DeclineGroupDuelCommand : IRequest<Result>
// {
//     public required Guid Id { get; init; }
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class DeclineGroupDuelHandler(Context context, ILogger<DeclineGroupDuelHandler> logger)
//     : IRequestHandler<DeclineGroupDuelCommand, Result>
// {
//     public async Task<Result> Handle(DeclineGroupDuelCommand command, CancellationToken cancellationToken)
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
//         var duel = await context.Duels.OfType<GroupDuel>()
//             .Include(d => d.Participants)
//             .Include(d => d.Group)
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
//         if (duel.IsConfirmed[user.Id])
//         {
//             return new ForbiddenError("Нельзя потдвердить участие в уже принятой дуэли в группе.");
//         }
//         
//         var otherUser = duel.Participants.Single(u => u.Id != user.Id);
//
//         duel.Decline(DateTime.UtcNow, user.Id);
//         
//         context.Duels.Remove(duel);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} declined duel in group {Group} with user {OtherNickname}",
//             user.Nickname, duel.Group.Id, otherUser.Nickname);
//
//         return Result.Ok();
//     }
// }
