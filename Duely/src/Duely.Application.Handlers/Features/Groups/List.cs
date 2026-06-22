// using Duely.Application.Handlers.Dto.Groups;
// using Duely.Domain.Kernel.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.Handlers.Features.Groups;
//
// public sealed class GetUserGroupsQuery : IRequest<Result<List<GroupShortDto>>>
// {
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class GetUserGroupsHandler(Context context)
//     : IRequestHandler<GetUserGroupsQuery, Result<List<GroupShortDto>>>
// {
//     public async Task<Result<List<GroupShortDto>>> Handle(GetUserGroupsQuery query, CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Where(u => u.Id == query.UserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//         
//         var groups = await context.Groups
//             .Include(g => g.Memberships)
//             .ThenInclude(m => m.User)
//             .Where(g => g.Memberships.Any(m => m.User.Id == query.UserId))
//             .ToListAsync(cancellationToken);
//         
//         return groups
//             .Select(g =>
//             {
//                 var membership = g.GetMembership(user)!;
//                 return new GroupShortDto
//                 {
//                     Id = g.Id,
//                     Name = g.Name,
//                     Membership = new GroupMembershipShortDto
//                     {
//                         Role = membership.Role,
//                         IsConfirmed = membership.IsConfirmed
//                     }
//                 };
//             })
//             .ToList();
//     }
// }
