// using System.Collections.ObjectModel;
// using Duely.Application.UseCases.Dto.Duels;
// using Duely.Application.UseCases.Dto.Users;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Domain.Models.Users.Entities;
// using Duely.Domain.Models.Users.Errors;
// using Duely.Domain.Services.Duels;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace Duely.Application.UseCases.Features.Duels.FriendlyDuels;
//
// public sealed class CreateFriendlyDuelCommand : IRequest<Result<FriendlyDuelDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid OtherUserId { get; init; }
//     public required Guid? DuelConfigurationId { get; init; }
// }
//
// internal sealed class CreateFriendlyDuelHandler(
//     Context context,
//     IOptions<DuelOptions> options,
//     ILogger<CreateFriendlyDuelHandler> logger)
//     : IRequestHandler<CreateFriendlyDuelCommand, Result<FriendlyDuelDto>>
// {
//     public async Task<Result<FriendlyDuelDto>> Handle(
//         CreateFriendlyDuelCommand command,
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
//         var otherUser = await context.Users
//             .AsNoTracking()
//             .Where(u => u.Id == command.OtherUserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (otherUser is null)
//         {
//             return new UserNotFoundError();
//         }
//         
//         var duelConfigurationResult = await ResolveDuelConfiguration(command.DuelConfigurationId, cancellationToken);
//         if (duelConfigurationResult.IsFailed)
//         {
//             return duelConfigurationResult.ToResult();
//         }
//         
//         var duelConfiguration = duelConfigurationResult.Value;
//         var id = Guid.NewGuid();
//         var participants = new Collection<User> {user, otherUser};
//         var duel = new FriendlyDuel(id, duelConfiguration, participants, DateTime.UtcNow, user);
//         
//         context.Duels.Add(duel);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} created friendly duel with user {OtherNickname}",
//             user.Nickname, otherUser.Nickname);
//
//         return new FriendlyDuelDto
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
//                     Nickname = p.Nickname
//                 })
//                 .ToList(),
//             // ProblemSet = duel.ProblemSet is null
//             //     ? null
//             //     : new ProblemSetDto
//             //     {
//             //         Problems = duel.ProblemSet.Problems
//             //             .Where(p => p.IsVisible)
//             //             .Select(p => new ProblemDto
//             //             {
//             //                 Position = p.Position,
//             //                 ExternalId = p.ExternalId
//             //             })
//             //             .ToList()
//             //     },
//             Status = duel.Status,
//             CreatedAt = duel.CreatedAt,
//             StartedAt = duel.StartedAt,
//             FinishedAt = duel.FinishedAt,
//             Winner = duel.Winner is null
//                 ? null
//                 : new UserShortDto
//                 {
//                     Id = duel.Winner.Id,
//                     Nickname = duel.Winner.Nickname
//                 },
//             CreatedBy = new UserShortDto
//             {
//                 Id = duel.CreatedBy.Id,
//                 Nickname = duel.CreatedBy.Nickname
//             },
//             IsConfirmed = duel.IsConfirmed
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
//         var duelConfigurationVersionId = Guid.NewGuid();
//         var duelConfigurationVersion = new DuelConfiguration(
//             duelConfigurationVersionId,
//             duelConfiguration?.IsRated ?? false,
//             duelConfiguration?.ShouldShowOpponentSolution ?? options.Value.DefaultShouldShowOpponentSolution,
//             duelConfiguration?.DurationMinutes ?? options.Value.DefaultDurationMinutes,
//             duelConfiguration?.ProblemsCount ?? options.Value.DefaultProblemsCount,
//             duelConfiguration?.ProblemsOrder ?? options.Value.DefaultProblemsOrder);
//
//         context.DuelConfigurations.Add(duelConfigurationVersion);
//         
//         return duelConfigurationVersion;
//     }
// }
//
// internal sealed class CreateFriendlyDuelCommandValidator : AbstractValidator<CreateFriendlyDuelCommand>
// {
//     public CreateFriendlyDuelCommandValidator()
//     {
//         RuleFor(x => x.OtherUserId)
//             .NotEqual(x => x.UserId)
//             .WithMessage("Нельзя создать дружескую дуэль с самим собой.");
//     }
// }
