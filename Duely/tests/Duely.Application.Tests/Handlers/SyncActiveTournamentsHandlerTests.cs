using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Tournaments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duely.Application.Tests.Handlers;

public sealed class SyncActiveTournamentsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creates_pending_tournament_duel_for_ready_match()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = creator, Group = group, Role = GroupRole.Creator });

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });
        tournament.Nodes = new List<SingleEliminationBracketNode?>
        {
            new SingleEliminationBracketNode(),
            new SingleEliminationBracketNode { UserId = user1.Id, WinnerUserId = user1.Id },
            new SingleEliminationBracketNode { UserId = user2.Id, WinnerUserId = user2.Id }
        };

        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var handler = new SyncActiveTournamentsHandler(
            Context,
            new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
            {
                new SingleEliminationBracketMatchmakingStrategy()
            }),
            NullLogger<SyncActiveTournamentsHandler>.Instance);

        var result = await handler.Handle(new SyncActiveTournamentsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var pending = await Context.PendingDuels
            .OfType<TournamentPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleAsync();
        pending.User1.Id.Should().Be(user1.Id);
        pending.User2.Id.Should().Be(user2.Id);
        pending.Tournament.Id.Should().Be(tournament.Id);

        var messages = await Context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        messages.Should().HaveCount(2);
        messages.Select(m => ((SendMessagePayload)m.Payload).Message)
            .Should().AllBeOfType<TournamentDuelInvitationMessage>();
    }

    [Fact]
    public async Task Does_not_create_duplicate_pending_duel_for_same_pair()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = creator, Group = group, Role = GroupRole.Creator });

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Nodes =
            [
                new SingleEliminationBracketNode(),
                new SingleEliminationBracketNode { UserId = user1.Id, WinnerUserId = user1.Id },
                new SingleEliminationBracketNode { UserId = user2.Id, WinnerUserId = user2.Id }
            ]
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });

        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        Context.PendingDuels.Add(new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new SyncActiveTournamentsHandler(
            Context,
            new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
            {
                new SingleEliminationBracketMatchmakingStrategy()
            }),
            NullLogger<SyncActiveTournamentsHandler>.Instance);

        var result = await handler.Handle(new SyncActiveTournamentsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await Context.PendingDuels.OfType<TournamentPendingDuel>().CountAsync()).Should().Be(1);
    }
}
