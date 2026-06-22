// using Duely.Application.Handlers.Dto.Duels;
// using Duely.Domain.Kernel.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.Handlers.Features.Duels;
//
// public sealed class GetDuelConfigurationsQuery : IRequest<Result<List<DuelConfigurationDto>>>
// {
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class GetDuelConfigurationsHandler(Context context)
//     : IRequestHandler<GetDuelConfigurationsQuery, Result<List<DuelConfigurationDto>>>
// {
//     public async Task<Result<List<DuelConfigurationDto>>> Handle(
//         GetDuelConfigurationsQuery query,
//         CancellationToken cancellationToken)
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
//         var configurations = await context.DuelConfigurations
//             .Where(configuration => configuration.CreatedBy != null && configuration.CreatedBy.Id == user.Id)
//             .OrderBy(configuration => configuration.Id)
//             .ToListAsync(cancellationToken);
//
//         return configurations
//             .Select(configuration => new DuelConfigurationDto
//             {
//                 Id = configuration.Id,
//                 ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
//                 DurationMinutes = configuration.DurationMinutes,
//                 ProblemsCount = configuration.ProblemsCount,
//                 ProblemsOrder = configuration.ProblemsOrder
//             })
//             .ToList();
//     }
// }
