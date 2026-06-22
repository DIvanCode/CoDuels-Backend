// using Duely.Application.Handlers.Dto.Tournaments;
// using Duely.Application.Handlers.Dto.Tournaments.Configurations.Factories;
// using Duely.Application.Handlers.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Models.Tournaments.Entities;
// using Duely.Domain.Models.Tournaments.Entities.Tournaments;
// using Duely.Domain.Models.Tournaments.Errors;
// using Duely.Domain.Services.Groups;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Tournaments.GroupTournaments;
//
// public sealed class StartGroupTournamentCommand : IRequest<Result<GroupTournamentDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
//     public required Guid TournamentId { get; init; }
// }
//
// public sealed class StartGroupTournamentHandler(
//     Context context,
//     IGroupPermissionsService groupPermissionsService,
//     ILogger<StartGroupTournamentHandler> logger,
//     ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
//     : IRequestHandler<StartGroupTournamentCommand, Result<GroupTournamentDto>>
// {
//     public async Task<Result<GroupTournamentDto>> Handle(
//         StartGroupTournamentCommand command,
//         CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Include(u => u.Nickname)
//             .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//         
//         var group = await context.Groups
//             .AsNoTracking()
//             .Include(g => g.Name)
//             .Include(g => g.Memberships.Where(m => m.User.Id == command.UserId))
//             .ThenInclude(m => m.User)
//             .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
//         if (group is null)
//         {
//             return new GroupNotFoundError();
//         }
//
//         var membership = group.GetMembership(user);
//         if (membership is null || !groupPermissionsService.CanStartTournament(membership))
//         {
//             return new ForbiddenError();
//         }
//         
//         var tournament = await context.Tournaments.OfType<GroupTournament>()
//             .Include(t => t.Name)
//             .Include(t => t.Configuration)
//             .Include(t => t.Group)
//             .ThenInclude(g => g.Name)
//             .Where(t => t.Id == command.TournamentId && t.Group.Id == command.GroupId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (tournament is null)
//         {
//             return new TournamentNotFoundError();
//         }
//         
//         if (tournament.Status != TournamentStatus.New)
//         {
//             return new ForbiddenError("Нельзя запустить уже запущенный турнир.");
//         }
//
//         tournament.Start();
//         
//         await context.SaveChangesAsync(cancellationToken);
//
//         logger.LogInformation(
//             "User {Nickname} started tournament {TournamentId} in group {GroupId}",
//             user.Nickname, tournament.Id, tournament.Group.Id);
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
