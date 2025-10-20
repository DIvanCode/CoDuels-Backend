using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.GetDuel;

public class GetDuelHandler : IRequestHandler<GetDuelQuery, Result<DuelDto>>
{
    private readonly Context _context;

    public GetDuelHandler(Context context)
    {
        _context = context;
    }

    public async Task<Result<DuelDto>> Handle(GetDuelQuery request, CancellationToken cancellationToken)
    {
        var duel = await _context.Duels
            .Where(d => d.Id == request.DuelId)
            .Select(d => new DuelDto
            {
                Id = d.Id,
                TaskId = d.TaskId,
                User1Id = d.User1Id,
                User2Id = d.User2Id,
                Status = d.Status,
                Result = d.Result,
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                MaxDuration = d.MaxDuration
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (duel is null)
        {
            return Result.Fail<DuelDto>($"Duel {request.DuelId} not found");
        }

        return Result.Ok(duel);
    }


}