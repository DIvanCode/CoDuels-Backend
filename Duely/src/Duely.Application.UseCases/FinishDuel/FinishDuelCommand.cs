using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.FinishDuel;
public sealed record FinishDuelCommand : IRequest<Result>{
    public required int DuelId{ get; init; }
    public required string Winner{ get; init; }
}
