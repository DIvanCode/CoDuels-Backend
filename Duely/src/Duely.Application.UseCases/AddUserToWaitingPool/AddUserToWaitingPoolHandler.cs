using Duely.Domain.Services;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.AddUserToWaitingPool;

public class AddUserToWaitingPoolHandler(IDuelManager duelManager) : IRequestHandler<AddUserToWaitingPoolCommand, Result>
{

    public Task<Result> Handle(AddUserToWaitingPoolCommand request, CancellationToken cancellationToken)
    {
        duelManager.AddUser(request.UserId);
        return Task.FromResult(Result.Ok());
    }
}