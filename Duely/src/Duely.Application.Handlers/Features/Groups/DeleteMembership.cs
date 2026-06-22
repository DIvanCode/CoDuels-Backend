// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Models.Users.Errors;
// using Duely.Domain.Services.Groups;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Groups;
//
// public sealed class DeleteGroupMembershipCommand : IRequest<Result>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
//     public required Guid TargetUserId { get; init; }
// }
//
// internal sealed class DeleteGroupMembershipHandler(
//     Context context,
//     IGroupPermissionsService groupPermissionsService,
//     ILogger<DeleteGroupMembershipHandler> logger)
//     : IRequestHandler<DeleteGroupMembershipCommand, Result>
// {
//     public async Task<Result> Handle(DeleteGroupMembershipCommand command, CancellationToken cancellationToken)
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
//         var group = await context.Groups
//             .AsNoTracking()
//             .Include(g => g.Memberships
//                 .Where(m => m.User.Id == command.UserId || m.User.Id == command.TargetUserId))
//             .ThenInclude(m => m.User)
//             .Where(g => g.Id == command.GroupId)
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
//         var targetUser = await context.Users
//             .AsNoTracking()
//             .Where(u => u.Id == command.TargetUserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (targetUser is null)
//         {
//             return new UserNotFoundError();
//         }
//
//         var targetMembership = group.GetMembership(targetUser);
//         if (targetMembership is null ||
//             (user.Id != targetUser.Id && !groupPermissionsService.CanDeleteMembership(membership, targetMembership)))
//         {
//             return new ForbiddenError();
//         }
//
//         group.DeleteMembership(targetMembership);
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} deleted membership of user {TargetNickname} in group {GroupId}",
//             user.Nickname, targetUser.Nickname, group.Id);
//
//         return Result.Ok();
//     }
// }
