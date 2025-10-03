using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.AddUserToWaitingPool;

public sealed class AddUserToWaitingPoolCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}