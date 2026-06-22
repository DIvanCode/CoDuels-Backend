// using Duely.Application.Handlers.Dto.Groups;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Groups;
//
// public sealed class ConfirmGroupMembershipCommand : IRequest<Result<GroupMembershipShortDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
// }
//
// internal sealed class ConfirmGroupMembershipHandler(Context context, ILogger<ConfirmGroupMembershipHandler> logger)
//     : IRequestHandler<ConfirmGroupMembershipCommand, Result<GroupMembershipShortDto>>
// {
//     public async Task<Result<GroupMembershipShortDto>> Handle(
//         ConfirmGroupMembershipCommand command,
//         CancellationToken cancellationToken)
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
//             .Include(g => g.Memberships.Where(m => m.User.Id == command.UserId))
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
//         if (membership.IsConfirmed)
//         {
//             return new ForbiddenError("Нельзя заново принять ранее принятое приглашение в группу.");
//         }
//
//         group.ConfirmMembership(membership);
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation("User {Nickname} confirmed membership in group {GroupId}", user.Nickname, group.Id);
//
//         return new GroupMembershipShortDto
//         {
//             Role = membership.Role,
//             IsConfirmed = membership.IsConfirmed
//         };
//     }
// }
