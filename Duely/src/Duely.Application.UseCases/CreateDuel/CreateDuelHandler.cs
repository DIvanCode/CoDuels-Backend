using Duely.Domain.Services;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.CreateDuel;

public class CreateDuelHandler(DuelManager duelManager) : IRequestHandler<CreateDuelCommand, Result>
{

    public Task<Result> Handle(CreateDuelCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Ok());
    }
}