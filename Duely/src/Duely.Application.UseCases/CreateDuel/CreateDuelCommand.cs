using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.CreateDuel;

public record CreateDuelCommand : IRequest<Result>
{
    public required string User1Id { get; init; }
    public required string User2Id { get; init; }
}