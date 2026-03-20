using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class GetIncomingTournamentDuelInvitationsQuery : IRequest<Result<List<TournamentDuelInvitationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetIncomingTournamentDuelInvitationsHandler(Context context)
    : IRequestHandler<GetIncomingTournamentDuelInvitationsQuery, Result<List<TournamentDuelInvitationDto>>>
{
    public async Task<Result<List<TournamentDuelInvitationDto>>> Handle(
        GetIncomingTournamentDuelInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        return await context.PendingDuels.OfType<TournamentPendingDuel>()
            .AsNoTracking()
            .Where(d =>
                (d.User1.Id == query.UserId && !d.IsAcceptedByUser1) ||
                (d.User2.Id == query.UserId && !d.IsAcceptedByUser2))
            .OrderBy(d => d.Id)
            .Select(d => new TournamentDuelInvitationDto
            {
                TournamentId = d.Tournament.Id,
                TournamentName = d.Tournament.Name,
                OpponentNickname = d.User1.Id == query.UserId ? d.User2.Nickname : d.User1.Nickname,
                ConfigurationId = d.Configuration != null ? d.Configuration.Id : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
