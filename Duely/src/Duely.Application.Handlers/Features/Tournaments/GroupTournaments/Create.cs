// using Duely.Application.Handlers.Dto.Tournaments;
// using Duely.Application.Handlers.Dto.Tournaments.Configurations.Factories;
// using Duely.Application.Handlers.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Models.Tournaments.Entities;
// using Duely.Domain.Models.Tournaments.Entities.Tournaments;
// using Duely.Domain.Models.Users.Errors;
// using Duely.Domain.Services.Duels;
// using Duely.Domain.Services.Groups;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace Duely.Application.Handlers.Features.Tournaments.GroupTournaments;
//
// public sealed class CreateGroupTournamentCommand : IRequest<Result<GroupTournamentDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
//     public required string Name { get; init; }
//     public required IReadOnlyList<Guid> Participants { get; init; }
//     public required TournamentConfigurationType ConfigurationType { get; init; }
//     public required Guid? DuelConfigurationId { get; init; }
// }
//
// public sealed class CreateGroupTournamentHandler(
//     Context context,
//     IGroupPermissionsService groupPermissionsService,
//     IOptions<DuelOptions> duelOptions,
//     ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory,
//     ILogger<CreateGroupTournamentHandler> logger)
//     : IRequestHandler<CreateGroupTournamentCommand, Result<GroupTournamentDto>>
// {
//     public async Task<Result<GroupTournamentDto>> Handle(
//         CreateGroupTournamentCommand command,
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
//             .Include(g => g.Memberships)
//             .ThenInclude(m => m.User)
//             .SingleOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
//         if (group is null)
//         {
//             return new GroupNotFoundError();
//         }
//         
//         var membership = group.GetMembership(user);
//         if (membership is null || !groupPermissionsService.CanCreateTournament(membership))
//         {
//             return new ForbiddenError();
//         }
//
//         var duelConfigurationResult = await ResolveDuelConfiguration(command.DuelConfigurationId, cancellationToken);
//         if (duelConfigurationResult.IsFailed)
//         {
//             return duelConfigurationResult.ToResult();
//         }
//         
//         var duelConfiguration = duelConfigurationResult.Value;
//         var configuration = TournamentConfiguration.Create(command.ConfigurationType, duelConfiguration);
//         var id = new TournamentId(Guid.NewGuid());
//         var name = new TournamentName(command.Name);
//         var tournament = new GroupTournament(id, name, user, DateTime.UtcNow, configuration, group);
//
//         foreach (var participantId in command.Participants)
//         {
//             var participant = await context.Users
//                 .AsNoTracking()
//                 .SingleOrDefaultAsync(u => u.Id == participantId, cancellationToken);
//             if (participant is null)
//             {
//                 return new UserNotFoundError();
//             }
//             
//             var participantMembership = group.GetMembership(participant);
//             if (participantMembership is null)
//             {
//                 return new ForbiddenError("Нельзя пригласить на турнир пользователя не из группы.");
//             }
//
//             tournament.AddParticipant(participant);
//         }
//
//         context.Tournaments.Add(tournament);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} created tournament {TournamentId} in group {GroupId}",
//             user.Nickname, tournament.Id, group.Id);
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
//     
//     private async Task<Result<DuelConfiguration>> ResolveDuelConfiguration(
//         Guid? duelConfigurationId,
//         CancellationToken cancellationToken)
//     {
//         DuelConfiguration? duelConfiguration = null;
//         if (duelConfigurationId is not null)
//         {
//             duelConfiguration = await context.DuelConfigurations
//                 .AsNoTracking()
//                 .SingleOrDefaultAsync(c => c.Id == duelConfigurationId, cancellationToken);
//             if (duelConfiguration is null)
//             {
//                 return new DuelConfigurationNotFoundError();
//             }
//         }
//         
//         var duelConfigurationVersionId = new DuelConfigurationId(Guid.NewGuid());
//         var duelConfigurationVersion = new DuelConfiguration(
//             duelConfigurationVersionId,
//             duelConfiguration?.IsRated ?? false,
//             duelConfiguration?.ShouldShowOpponentSolution ?? duelOptions.Value.DefaultShouldShowOpponentSolution,
//             duelConfiguration?.DurationMinutes ?? duelOptions.Value.DefaultDurationMinutes,
//             duelConfiguration?.ProblemsCount ?? duelOptions.Value.DefaultProblemsCount,
//             duelConfiguration?.ProblemsOrder ?? duelOptions.Value.DefaultProblemsOrder);
//
//         context.DuelConfigurations.Add(duelConfigurationVersion);
//         
//         return duelConfigurationVersion;
//     }
// }
//
// public sealed class CreateGroupTournamentCommandValidator : AbstractValidator<CreateGroupTournamentCommand>
// {
//     public CreateGroupTournamentCommandValidator()
//     {
//         RuleFor(x => x.Name)
//             .NotEmpty().WithMessage("Название турнира не может быть пустым.");
//         
//         RuleFor(x => x.Name)
//             .MaximumLength(TournamentName.MaxLength)
//             .WithMessage($"Название турнира не может содержать более {TournamentName.MaxLength} символов.");
//
//         RuleFor(r => r.Participants)
//             .Must(p => p.Distinct().Count() >= 2)
//             .WithMessage("Турнир должен содержать хотя бы двух различных участников.");
//     }
// }
