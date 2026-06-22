// using Duely.Application.Handlers.Dto.Tournaments;
// using Duely.Application.Handlers.Dto.Tournaments.Configurations.Factories;
// using Duely.Application.Handlers.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Models.Tournaments.Entities.Tournaments;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.Handlers.Features.Tournaments.GroupTournaments;
//
// public sealed class GetGroupTournamentsQuery : IRequest<Result<List<GroupTournamentDto>>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
// }
//
// public sealed class GetGroupTournamentsHandler(
//     Context context,
//     ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
//     : IRequestHandler<GetGroupTournamentsQuery, Result<List<GroupTournamentDto>>>
// {
//     public async Task<Result<List<GroupTournamentDto>>> Handle(
//         GetGroupTournamentsQuery query,
//         CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Include(u => u.Nickname)
//             .SingleOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//         
//         var group = await context.Groups
//             .AsNoTracking()
//             .Include(g => g.Name)
//             .Include(g => g.Memberships.Where(m => m.User.Id == query.UserId))
//             .ThenInclude(m => m.User)
//             .SingleOrDefaultAsync(g => g.Id == query.GroupId, cancellationToken);
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
//         var tournaments = await context.Tournaments.OfType<GroupTournament>()
//             .AsNoTracking()
//             .Include(t => t.Name)
//             .Include(t => t.Configuration)
//             .Include(t => t.Group)
//             .ThenInclude(g => g.Name)
//             .Include(t => t.CreatedBy)
//             .ThenInclude(u => u.Nickname)
//             .Include(t => t.Participants)
//             .ThenInclude(p => p.User)
//             .ThenInclude(u => u.Nickname)
//             .Where(t => t.Group.Id == query.GroupId)
//             .ToListAsync(cancellationToken);
//
//         return tournaments
//             .Select(tournament => new GroupTournamentDto
//             {
//                 Id = tournament.Id,
//                 Name = tournament.Name.Value,
//                 Type = tournament.Type,
//                 Status = tournament.Status,
//                 CreatedBy = new UserShortDto
//                 {
//                     Id = tournament.CreatedBy.Id,
//                     Nickname = tournament.CreatedBy.Nickname.Value
//                 },
//                 CreatedAt = tournament.CreatedAt,
//                 Participants = tournament.Participants
//                     .Select(p => new UserShortDto
//                     {
//                         Id = p.User.Id,
//                         Nickname = p.User.Nickname.Value
//                     })
//                     .ToList(),
//                 Configuration = tournamentConfigurationDtoFactory.Create(tournament.Configuration)
//             })
//             .ToList();
//     }
// }
