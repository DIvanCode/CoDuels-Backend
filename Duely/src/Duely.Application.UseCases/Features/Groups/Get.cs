// using Duely.Application.UseCases.Dto.Groups;
// using Duely.Application.UseCases.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.UseCases.Features.Groups;
//
// public sealed class GetGroupQuery : IRequest<Result<GroupDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
// }
//
// internal sealed class GetGroupHandler(Context context) : IRequestHandler<GetGroupQuery, Result<GroupDto>>
// {
//     public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
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
//         var group = await context.Groups
//             .AsNoTracking()
//             .Include(g => g.Memberships)
//             .ThenInclude(m => m.User)
//             .Where(g => g.Id == query.GroupId)
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
//         return new GroupDto
//         {
//             Id = group.Id,
//             Name = group.Name,
//             Memberships = group.Memberships
//                 .Select(m => new GroupMembershipDto
//                 {
//                     User = new UserShortDto
//                     {
//                         Id = m.User.Id,
//                         Nickname = m.User.Nickname
//                     },
//                     Role = m.Role,
//                     IsConfirmed = m.IsConfirmed
//                 })
//                 .ToList()
//         };
//     }
// }
