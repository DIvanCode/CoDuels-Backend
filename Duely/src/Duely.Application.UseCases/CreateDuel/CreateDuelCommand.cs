using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.CreateDuel;

public record CreateDuelCommand : IRequest<Result>
{
    public required int User1Id { get; init; }
    public required int User2Id { get; init; }
}