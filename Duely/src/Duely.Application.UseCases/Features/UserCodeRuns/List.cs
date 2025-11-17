using Duely.Application.UseCases.Dtos;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.UserCodeRuns;

public sealed class GetUserCodeRunsQuery : IRequest<Result<List<UserCodeRunListItemDto>>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
}

public sealed class GetUserCodeRunsHandler(Context context) : IRequestHandler<GetUserCodeRunsQuery, Result<List<UserCodeRunListItemDto>>>
{
    public async Task<Result<List<UserCodeRunListItemDto>>> Handle(GetUserCodeRunsQuery query, CancellationToken cancellationToken)
    {
        var runs = await context.UserCodeRuns
            .Include(r => r.Duel)
            .Include(r => r.User)
            .Where(r => r.Duel.Id == query.DuelId && r.User.Id == query.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var dto = runs.Select(r => new UserCodeRunListItemDto
        {
            RunId = r.Id,
            Status = r.Status,
            Verdict = r.Verdict,
            CreatedAt = r.CreatedAt
        }).ToList();

        return dto;
    }
}
