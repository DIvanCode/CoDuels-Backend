// using Duely.Application.UseCases.Dto.Tournaments;
// using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
// using Duely.Application.UseCases.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Models.Tournaments.Entities.Tournaments;
// using Duely.Domain.Models.Tournaments.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.UseCases.Features.Tournaments.GroupTournaments;
//
// public sealed class GetGroupTournamentQuery : IRequest<Result<GroupTournamentDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
//     public required Guid TournamentId { get; init; }
// }
//
// public sealed class GetGroupTournamentHandler(
//     Context context,
//     ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
//     : IRequestHandler<GetGroupTournamentQuery, Result<GroupTournamentDto>>
// {
//     public async Task<Result<GroupTournamentDto>> Handle(
//         GetGroupTournamentQuery query,
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
//         var tournament = await context.Tournaments.OfType<GroupTournament>()
//             .AsNoTracking()
//             .Include(t => t.Name)
//             .Include(t => t.Configuration)
//             .Include(t => t.Group)
//             .ThenInclude(g => g.Name)
//             .Where(t => t.Id == query.TournamentId && t.Group.Id == query.GroupId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (tournament is null)
//         {
//             return new TournamentNotFoundError();
//         }
//
//         return new GroupTournamentDto
//         {
//             Id = tournament.Id,
//             Name = tournament.Name.Value,
//             Type = tournament.Type,
//             Status = tournament.Status,
//             CreatedBy = new UserShortDto
//             {
//                 Id = tournament.CreatedBy.Id,
//                 Nickname = tournament.CreatedBy.Nickname.Value
//             },
//             CreatedAt = tournament.CreatedAt,
//             Participants = tournament.Participants
//                 .Select(p => new UserShortDto
//                 {
//                     Id = p.User.Id,
//                     Nickname = p.User.Nickname.Value
//                 })
//                 .ToList(),
//             Configuration = tournamentConfigurationDtoFactory.Create(tournament.Configuration)
//         };
//     }
// }
