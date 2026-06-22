// using Duely.Application.UseCases.Dto.Duels;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.UseCases.Features.Duels;
//
// public sealed class CreateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
// {
//     public required Guid UserId { get; init; }
//     public required bool ShouldShowOpponentSolution { get; init; }
//     public required int DurationMinutes { get; init; }
//     public required int ProblemsCount { get; init; }
//     public required ProblemsOrder ProblemsOrder { get; init; }
// }
//
// internal sealed class CreateDuelConfigurationHandler(Context context, ILogger<CreateDuelConfigurationHandler> logger)
//     : IRequestHandler<CreateDuelConfigurationCommand, Result<DuelConfigurationDto>>
// {
//     public async Task<Result<DuelConfigurationDto>> Handle(
//         CreateDuelConfigurationCommand command,
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
//         var id = Guid.NewGuid();
//         var configuration = new DuelConfiguration(
//             id,
//             isRated: false,
//             command.ShouldShowOpponentSolution,
//             command.DurationMinutes,
//             command.ProblemsCount,
//             command.ProblemsOrder,
//             user);
//
//         context.DuelConfigurations.Add(configuration);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation("User {Nickname} created duel configuration {Id}", user.Nickname, id);
//
//         return new DuelConfigurationDto
//         {
//             Id = configuration.Id,
//             ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
//             DurationMinutes = configuration.DurationMinutes,
//             ProblemsCount = configuration.ProblemsCount,
//             ProblemsOrder = configuration.ProblemsOrder
//         };
//     }
// }
//
// internal sealed class CreateDuelConfigurationCommandValidator : AbstractValidator<CreateDuelConfigurationCommand>
// {
//     public CreateDuelConfigurationCommandValidator()
//     {
//         RuleFor(c => c.DurationMinutes)
//             .GreaterThan(0)
//             .WithMessage("Длительность дуэли не может быть меньше либо равна 0.");
//         RuleFor(c => c.DurationMinutes)
//             .LessThanOrEqualTo(DuelConstants.Configuration.MaxDurationMinutes)
//             .WithMessage($"Длительность дуэли не может превышать {DuelConstants.Configuration.MaxDurationMinutes} минут.");
//         
//         RuleFor(c => c.ProblemsCount)
//             .GreaterThan(0)
//             .WithMessage("Дуэль должна содержать хотя бы одну задачу.");
//         RuleFor(c => c.ProblemsCount)
//             .LessThanOrEqualTo(DuelConstants.Configuration.MaxProblemsCount)
//             .WithMessage($"Дуэль не может содержать больше {DuelConstants.Configuration.MaxProblemsCount} задач.");
//     }
// }
