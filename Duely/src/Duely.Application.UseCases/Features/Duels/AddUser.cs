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

public sealed class AddUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class AddUserHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<AddUserCommand, Result>
{
    public async Task<Result> Handle(AddUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        duelManager.AddUser(user.Id, user.Rating);
        return Result.Ok();
    }
}
