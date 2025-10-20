using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.GetDuel;

public sealed class GetDuelQuery : IRequest<Result<DuelDto>>
{
    public required int DuelId { get; init; }
}
