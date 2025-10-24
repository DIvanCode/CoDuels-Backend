using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class AddUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class AddUserHandler(IDuelManager duelManager) : IRequestHandler<AddUserCommand, Result>
{
    public Task<Result> Handle(AddUserCommand command, CancellationToken cancellationToken)
    {
        duelManager.AddUser(command.UserId);
        return Task.FromResult(Result.Ok());
    }
}
