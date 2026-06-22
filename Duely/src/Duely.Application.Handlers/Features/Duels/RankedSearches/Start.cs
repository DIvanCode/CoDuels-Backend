// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Duels.RankedSearches;
//
// public sealed class StartRankedSearchCommand : IRequest<Result>
// {
//     public required Guid UserId { get; init; }
// }
//
// internal sealed class StartRankedSearchHandler(Context context, ILogger<StartRankedSearchHandler> logger)
//     : IRequestHandler<StartRankedSearchCommand, Result>
// {
//     public async Task<Result> Handle(StartRankedSearchCommand command, CancellationToken cancellationToken)
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
//         var rankedSearchExists = await context.RankedSearches
//             .AsNoTracking()
//             .Where(s => s.User.Id == command.UserId)
//             .AnyAsync(cancellationToken);
//         if (rankedSearchExists)
//         {
//             return new EntityAlreadyExistsError("Вы уже находитесь в поиске рейтинговой дуэли.");
//         }
//
//         var rankedSearch = new RankedSearch(user, user.Rating, DateTime.UtcNow);
//         
//         context.RankedSearches.Add(rankedSearch);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation(
//             "User {Nickname} started ranked search with rating {Rating}",
//             user.Nickname, user.Rating);
//         
//         return Result.Ok();
//     }
// }
