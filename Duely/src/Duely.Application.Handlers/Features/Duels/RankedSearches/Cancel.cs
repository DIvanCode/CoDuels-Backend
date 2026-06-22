// using Duely.Domain.Kernel.Errors;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Duels.RankedSearches;
//
// public sealed class CancelRankedSearchCommand : IRequest<Result>
// {
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class CancelRankedSearchHandler(
//     Context context,
//     ILogger<CancelRankedSearchHandler> logger)
//     : IRequestHandler<CancelRankedSearchCommand, Result>
// {
//     public async Task<Result> Handle(CancelRankedSearchCommand command, CancellationToken cancellationToken)
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
//         var rankedSearch = await context.RankedSearches
//             .Where(s => s.User.Id == command.UserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (rankedSearch is null)
//         {
//             return new EntityNotFoundError("Вы не находитесь в поиске рейтинговой дуэли.");
//         }
//
//         rankedSearch.Cancel();
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation("User {Nickname} canceled ranked search", user.Nickname);
//         
//         return Result.Ok();
//     }
// }
