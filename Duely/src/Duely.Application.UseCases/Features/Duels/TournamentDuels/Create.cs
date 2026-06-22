// using Duely.Application.UseCases.Dto.Duels;
// using Duely.Application.UseCases.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Tournaments.Errors;
// using Duely.Domain.Models.Users.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.UseCases.Features.Duels.TournamentDuels;
//
// public sealed class CreateTournamentDuelCommand : IRequest<Result<TournamentDuelDto>>
// {
//     public required Guid TournamentId { get; init; }
//     public required IReadOnlyCollection<Guid> Participants { get; init; }
// }
//
// internal sealed class CreateTournamentDuelHandler(
//     Context context,
//     ILogger<CreateTournamentDuelHandler> logger)
//     : IRequestHandler<CreateTournamentDuelCommand, Result<TournamentDuelDto>>
// {
//     public async Task<Result<TournamentDuelDto>> Handle(CreateTournamentDuelCommand command, CancellationToken cancellationToken)
//     {
//         var tournament = await context.Tournaments
//             .AsNoTracking()
//             .Include(t => t.Participants)
//             .ThenInclude(p => p.User)
//             .Include(t => t.Configuration)
//             .ThenInclude(c => c.DuelConfiguration)
//             .SingleOrDefaultAsync(t => t.Id == command.TournamentId, cancellationToken);
//         if (tournament is null)
//         {
//             return new TournamentNotFoundError();
//         }
//         
//         var participants = await context.Users
//             .AsNoTracking()
//             .Include(u => u.Nickname)
//             .Include(u => u.Rating)
//             .Where(u => command.Participants.Contains(u.Id))
//             .ToListAsync(cancellationToken);
//         if (participants.Count != 2)
//         {
//             return new UserNotFoundError();
//         }
//
//         foreach (var participant in participants)
//         {
//             if (tournament.Participants.All(p => p.User.Id != participant.Id))
//             {
//                 return new ForbiddenError("Создать дуэль можно только между участниками турнира.");
//             }
//         }
//         
//         var duelConfiguration = tournament.Configuration.DuelConfiguration;
//         var id = new DuelId(Guid.NewGuid());
//         var duel = new TournamentDuel(id, duelConfiguration, participants, DateTime.UtcNow, tournament);
//         
//         context.Duels.Add(duel);
//         await context.SaveChangesAsync(cancellationToken);
//
//         logger.LogInformation(
//             "Created tournament duel with users {Participants} in tournament {Tournament}",
//             string.Join(", ", participants.Select(p => p.Nickname)), tournament.Id);
//
//         return new TournamentDuelDto
//         {
//             Id = duel.Id,
//             Type = duel.Type,
//             Configuration = new DuelConfigurationDto
//             {
//                 Id = duel.Configuration.Id,
//                 ShouldShowOpponentSolution = duel.Configuration.ShouldShowOpponentSolution,
//                 DurationMinutes = duel.Configuration.DurationMinutes,
//                 ProblemsCount = duel.Configuration.ProblemsCount,
//                 ProblemsOrder = duel.Configuration.ProblemsOrder
//             },
//             Participants = duel.Participants
//                 .Select(p => new UserShortDto
//                 {
//                     Id = p.Id,
//                     Nickname = p.Nickname.Value
//                 })
//                 .ToList(),
//             ProblemSet = duel.ProblemSet is null
//                 ? null
//                 : new ProblemSetDto
//                 {
//                     Problems = duel.ProblemSet.Problems
//                         .Where(p => p.IsVisible)
//                         .Select(p => new ProblemDto
//                         {
//                             Position = p.Position,
//                             ExternalId = p.ExternalId
//                         })
//                         .ToList()
//                 },
//             Status = duel.Status,
//             CreatedAt = duel.CreatedAt,
//             StartedAt = duel.StartedAt,
//             FinishedAt = duel.FinishedAt,
//             Winner = duel.Winner is null
//                 ? null
//                 : new UserShortDto
//                 {
//                     Id = duel.Winner.Id,
//                     Nickname = duel.Winner.Nickname.Value
//                 },
//             IsConfirmed = duel.IsConfirmed
//                 .ToDictionary(x => x.Key.Value, x => x.Value)
//         };
//     }
// }
//
// internal sealed class CreateTournamentDuelCommandValidator : AbstractValidator<CreateTournamentDuelCommand>
// {
//     public CreateTournamentDuelCommandValidator()
//     {
//         RuleFor(x => x.Participants)
//             .Must(x => x.Distinct().Count() == 2)
//             .WithMessage("Дуэль в группе должна быть между двумя разными пользователями.");
//     }
// }
