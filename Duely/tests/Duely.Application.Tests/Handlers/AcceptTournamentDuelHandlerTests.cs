using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Tournaments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class AcceptTournamentDuelHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = 1,
            TournamentId = 1
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Accepts_when_tournament_pending_exists()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        Context.Users.AddRange(creator, user1, user2);
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

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user1.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<TournamentPendingDuel>()
            .Should().ContainSingle(d => d.IsAcceptedByUser1 && !d.IsAcceptedByUser2);
    }

    [Fact]
    public async Task Accepts_when_second_user_accepts()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        Context.Users.AddRange(creator, user1, user2);
        Context.Tournaments.Add(tournament);
        Context.PendingDuels.Add(new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user2.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<TournamentPendingDuel>()
            .Should().ContainSingle(d => d.IsAcceptedByUser1 && d.IsAcceptedByUser2);
    }

    [Fact]
    public async Task AlreadyExists_when_user_has_active_duel()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(user1, user2);
        Context.Duels.Add(EntityFactory.MakeDuel(10, user1, user2));
        await Context.SaveChangesAsync();

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user1.Id,
            TournamentId = 1
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task NotFound_when_pending_missing()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        Context.Users.AddRange(creator, user1, user2);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user1.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Cancels_ranked_pending_and_sends_no_message()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        Context.Users.AddRange(creator, user1, user2);
        Context.Tournaments.Add(tournament);
        Context.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user1,
                Rating = user1.Rating,
                CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = user1,
                User2 = user2,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user1.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();
        (await Context.OutboxMessages.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancels_outgoing_friendly_invitation_when_accepting()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var other = EntityFactory.MakeUser(4, "u3");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        Context.Users.AddRange(creator, user1, user2, other);
        Context.Tournaments.Add(tournament);
        Context.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = user1,
                User2 = other,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = user1,
                User2 = user2,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var handler = new AcceptTournamentDuelHandler(Context);

        var res = await handler.Handle(new AcceptTournamentDuelCommand
        {
            UserId = user1.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<FriendlyPendingDuel>().Should().BeEmpty();

        var outboxMessage = await Context.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = (SendMessagePayload)outboxMessage.Payload;
        payload.UserId.Should().Be(user1.Id);
        payload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(other.Nickname);
    }
}
