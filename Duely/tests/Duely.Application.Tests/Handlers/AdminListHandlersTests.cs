using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Groups;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Moq;

namespace Duely.Application.Tests.Handlers;

public sealed class AdminListHandlersTests : ContextBasedTest
{
    [Fact]
    public async Task GetUsers_filters_connected_ids_and_orders_by_creation_time()
    {
        var older = MakeUser(1, "older", DateTime.UtcNow.AddDays(-2));
        var newer = MakeUser(2, "newer", DateTime.UtcNow.AddDays(-1));
        var disconnected = MakeUser(3, "disconnected", DateTime.UtcNow);
        Context.Users.AddRange(older, newer, disconnected);
        await Context.SaveChangesAsync();

        var handler = new GetUsersHandler(Context);
        var result = await handler.Handle(
            new GetUsersQuery { UserIds = [older.Id, newer.Id] },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(user => user.Id).Should().Equal(newer.Id, older.Id);

        var emptyResult = await handler.Handle(
            new GetUsersQuery { UserIds = [] },
            CancellationToken.None);
        emptyResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingDuels_returns_non_ranked_types_and_ranked_searchers_separately()
    {
        var user1 = MakeUser(1, "u1", DateTime.UtcNow.AddDays(-2));
        var user2 = MakeUser(2, "u2", DateTime.UtcNow.AddDays(-1));
        var group = EntityFactory.MakeGroup(1, "Group");
        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Tournament",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = user1,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        var oldest = DateTime.UtcNow.AddMinutes(-4);
        var friendly = new FriendlyPendingDuel
        {
            Id = 1,
            Type = PendingDuelType.Friendly,
            CreatedAt = oldest,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAccepted = false
        };
        var groupDuel = new GroupPendingDuel
        {
            Id = 2,
            Type = PendingDuelType.Group,
            CreatedAt = oldest.AddMinutes(1),
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = false
        };
        var tournamentDuel = new TournamentPendingDuel
        {
            Id = 3,
            Type = PendingDuelType.Tournament,
            CreatedAt = oldest.AddMinutes(2),
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = true
        };
        var ranked = new RankedPendingDuel
        {
            Id = 4,
            Type = PendingDuelType.Ranked,
            CreatedAt = oldest.AddMinutes(3),
            User = user2,
            Rating = 1700
        };

        Context.Users.AddRange(user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        Context.PendingDuels.AddRange(friendly, groupDuel, tournamentDuel, ranked);
        await Context.SaveChangesAsync();

        var pendingResult = await new GetPendingDuelsHandler(Context)
            .Handle(new GetPendingDuelsQuery(), CancellationToken.None);
        var searchersResult = await new GetRankedDuelSearchersHandler(Context)
            .Handle(new GetRankedDuelSearchersQuery(), CancellationToken.None);

        pendingResult.Value.Select(duel => duel.Type).Should().Equal(
            PendingDuelType.Tournament,
            PendingDuelType.Group,
            PendingDuelType.Friendly);
        pendingResult.Value[0].TournamentName.Should().Be(tournament.Name);
        pendingResult.Value[1].GroupName.Should().Be(group.Name);
        pendingResult.Value[2].IsAcceptedByUser1.Should().BeTrue();
        pendingResult.Value[2].IsAcceptedByUser2.Should().BeFalse();
        searchersResult.Value.Should().ContainSingle();
        searchersResult.Value[0].User.Id.Should().Be(user2.Id);
        searchersResult.Value[0].Rating.Should().Be(1700);
        searchersResult.Value[0].SearchStartedAt.Should().Be(ranked.CreatedAt);
    }

    [Fact]
    public async Task GetDuels_filters_status_orders_by_start_time_and_maps_for_admin_viewer()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var older = EntityFactory.MakeDuel(1, user1, user2, start: DateTime.UtcNow.AddHours(-2));
        var newer = EntityFactory.MakeDuel(2, user1, user2, start: DateTime.UtcNow.AddHours(-1));
        var finished = EntityFactory.MakeDuel(3, user1, user2, start: DateTime.UtcNow.AddHours(-3));
        finished.Status = DuelStatus.Finished;
        finished.EndTime = DateTime.UtcNow.AddHours(-2);
        Context.Users.AddRange(user1, user2);
        Context.Duels.AddRange(older, newer, finished);
        await Context.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager
            .Setup(manager => manager.GetRatingChanges(
                It.IsAny<Duel>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .Returns([]);
        var handler = new GetDuelsByStatusHandler(Context, ratingManager.Object, new TaskService());

        var result = await handler.Handle(
            new GetDuelsByStatusQuery { Status = DuelStatus.InProgress },
            CancellationToken.None);

        result.Value.Select(duel => duel.Id).Should().Equal(newer.Id, older.Id);
        result.Value.Should().OnlyContain(duel => duel.OpponentSolutions != null);
    }

    [Fact]
    public async Task GetSubmissions_returns_testing_and_all_lists_ordered_by_submit_time()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(1, user1, user2);
        var queued = EntityFactory.MakeSubmission(
            1,
            duel,
            user1,
            time: DateTime.UtcNow.AddMinutes(-3),
            status: SubmissionStatus.Queued);
        var running = EntityFactory.MakeSubmission(
            2,
            duel,
            user2,
            time: DateTime.UtcNow.AddMinutes(-2),
            status: SubmissionStatus.Running);
        var done = EntityFactory.MakeSubmission(
            3,
            duel,
            user1,
            time: DateTime.UtcNow.AddMinutes(-1),
            status: SubmissionStatus.Done);
        Context.Users.AddRange(user1, user2);
        Context.Duels.Add(duel);
        Context.Submissions.AddRange(queued, running, done);
        await Context.SaveChangesAsync();

        var handler = new GetAllSubmissionsHandler(Context);
        var testingResult = await handler.Handle(
            new GetAllSubmissionsQuery { OnlyTesting = true },
            CancellationToken.None);
        var allResult = await handler.Handle(new GetAllSubmissionsQuery(), CancellationToken.None);

        testingResult.Value.Select(submission => submission.SubmissionId).Should().Equal(running.Id, queued.Id);
        allResult.Value.Select(submission => submission.SubmissionId).Should().Equal(done.Id, running.Id, queued.Id);
        allResult.Value.Should().OnlyContain(submission => submission.DuelId == duel.Id);
        allResult.Value.Should().OnlyContain(submission => submission.TaskKey == 'A');
    }

    [Fact]
    public async Task GetGroups_orders_all_groups_by_name()
    {
        Context.Groups.AddRange(
            EntityFactory.MakeGroup(1, "Zulu"),
            EntityFactory.MakeGroup(2, "Alpha"));
        await Context.SaveChangesAsync();

        var result = await new GetAllGroupsHandler(Context)
            .Handle(new GetAllGroupsQuery(), CancellationToken.None);

        result.Value.Select(group => group.Name).Should().Equal("Alpha", "Zulu");
    }

    [Fact]
    public async Task GetTournaments_splits_active_and_finished_and_orders_by_creation_time()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var group = EntityFactory.MakeGroup(1, "Group");
        var olderActive = MakeTournament(1, "New", TournamentStatus.New, DateTime.UtcNow.AddHours(-3), group, creator);
        var newerActive = MakeTournament(
            2,
            "In progress",
            TournamentStatus.InProgress,
            DateTime.UtcNow.AddHours(-2),
            group,
            creator);
        var finished = MakeTournament(
            3,
            "Finished",
            TournamentStatus.Finished,
            DateTime.UtcNow.AddHours(-1),
            group,
            creator);
        Context.Users.Add(creator);
        Context.Groups.Add(group);
        Context.Tournaments.AddRange(olderActive, newerActive, finished);
        await Context.SaveChangesAsync();

        var handler = new GetTournamentsByStateHandler(Context);
        var activeResult = await handler.Handle(
            new GetTournamentsByStateQuery { Finished = false },
            CancellationToken.None);
        var finishedResult = await handler.Handle(
            new GetTournamentsByStateQuery { Finished = true },
            CancellationToken.None);

        activeResult.Value.Select(tournament => tournament.Id).Should().Equal(newerActive.Id, olderActive.Id);
        finishedResult.Value.Should().ContainSingle().Which.Id.Should().Be(finished.Id);
    }

    private static User MakeUser(int id, string nickname, DateTime createdAt)
    {
        return new User
        {
            Id = id,
            Nickname = nickname,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rating = 1500,
            CreatedAt = createdAt
        };
    }

    private static Tournament MakeTournament(
        int id,
        string name,
        TournamentStatus status,
        DateTime createdAt,
        Group group,
        User creator)
    {
        return new SingleEliminationBracketTournament
        {
            Id = id,
            Name = name,
            Status = status,
            Group = group,
            CreatedBy = creator,
            CreatedAt = createdAt,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
    }
}
