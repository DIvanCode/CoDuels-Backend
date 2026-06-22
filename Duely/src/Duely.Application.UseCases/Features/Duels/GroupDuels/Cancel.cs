// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Services.Groups;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.UseCases.Features.Duels.GroupDuels;
//
// public sealed class CancelGroupDuelCommand : IRequest<Result>
// {
//     public required Guid Id { get; init; }
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class CancelGroupDuelHandler(
//     Context context,
//     IGroupPermissionsService groupPermissionsService,
//     ILogger<CancelGroupDuelHandler> logger)
//     : IRequestHandler<CancelGroupDuelCommand, Result>
// {
//     public async Task<Result> Handle(CancelGroupDuelCommand command, CancellationToken cancellationToken)
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
//         var group = await context.Groups
//             .AsNoTracking()
//             .Include(g => g.Memberships.Where(m => m.User.Id == user.Id))
//             .ThenInclude(m => m.User)
//             .Where(g => g.Id == duel.Group.Id)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (group is null)
//         {
//             return new GroupNotFoundError();
//         }
//
//         var membership = group.GetMembership(user);
//         if (membership is null)
//         {
//             return new ForbiddenError();            
//         }
//         
//         if (!groupPermissionsService.CanDeleteDuel(membership))
//         {
//             return new ForbiddenError("У вас недостаточно прав для отмены дуэли в этой группе.");
//         }
//         
//         if (duel.Status != DuelStatus.Pending)
//         {
//             return new ForbiddenError("Нельзя отменить начатую дуэль в группе.");
//         }
//
//         duel.Cancel();
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} canceled duel in group {Group} with users {Participants}",
//             user.Nickname, duel.Group.Id, string.Join(", ", duel.Participants.Select(p => p.Nickname)));
//
//         return Result.Ok();
//     }
// }
