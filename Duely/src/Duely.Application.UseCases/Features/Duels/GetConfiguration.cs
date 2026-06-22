// using Duely.Application.UseCases.Dto.Duels;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Duely.Application.UseCases.Features.Duels;
//
// public sealed class GetDuelConfigurationQuery : IRequest<Result<DuelConfigurationDto>>
// {
//     public required Guid Id { get; init; }
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class GetDuelConfigurationHandler(Context context)
//     : IRequestHandler<GetDuelConfigurationQuery, Result<DuelConfigurationDto>>
// {
//     public async Task<Result<DuelConfigurationDto>> Handle(
//         GetDuelConfigurationQuery query,
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
//         var configuration = await context.DuelConfigurations
//             .AsNoTracking()
//             .Include(c => c.CreatedBy)
//             .Where(c => c.Id == query.Id)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (configuration is null)
//         {
//             return new DuelConfigurationNotFoundError();
//         }
//
//         if (configuration.CreatedBy is not null && configuration.CreatedBy.Id != user.Id)
//         {
//             return new ForbiddenError();
//         }
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
