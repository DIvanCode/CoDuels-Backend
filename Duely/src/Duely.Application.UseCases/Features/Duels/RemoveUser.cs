using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Errors;
using System.Threading;
using System.Threading.Tasks;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class RemoveUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class RemoveUserHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<RemoveUserCommand, Result>
{
    public async Task<Result> Handle(RemoveUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (!duelManager.IsUserWaiting(user.Id))
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.RemoveUser(user.Id);
        Console.WriteLine($"Removed user {user.Id}");
        
        return Result.Ok();
    }
}
