using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class GlobalTournamentHandlerTests : ContextBasedTest
{
    private CreateTournamentHandler CreateHandler() => new(
        Context,
        new GroupPermissionsService(),
        new TournamentMatchmakingStrategyResolver(Array.Empty<ITournamentMatchmakingStrategy>()));

    [Fact]
    public async Task Admin_creates_global_tournament_without_group_or_participants()
    {
        var admin = EntityFactory.MakeUser(1, "admin");
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();
        var start = DateTime.UtcNow.AddHours(2);

        var result = await CreateHandler().Handle(new CreateTournamentCommand
        {
            UserId = admin.Id,
            IsAdmin = true,
            Name = "World Cup",
            Description = "Open tournament",
            StartsAt = start,
            MatchmakingType = TournamentMatchmakingType.Swiss
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(TournamentKind.Global);
        result.Value.GroupId.Should().BeNull();
        result.Value.Description.Should().Be("Open tournament");
        result.Value.StartsAt.Should().Be(start);
        result.Value.Participants.Should().BeEmpty();
        (await Context.Tournaments.SingleAsync()).Should().BeOfType<SwissTournament>();
    }

    [Fact]
    public async Task Non_admin_cannot_create_global_tournament()
    {
        var result = await CreateHandler().Handle(new CreateTournamentCommand
        {
            UserId = 1,
            Name = "World Cup",
            Description = "Open tournament",
            StartsAt = DateTime.UtcNow.AddHours(1),
            MatchmakingType = TournamentMatchmakingType.Swiss
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);
        (await Context.Tournaments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Any_authenticated_user_can_list_and_view_global_tournament_before_start()
    {
        var admin = EntityFactory.MakeUser(1, "admin");
        var player = EntityFactory.MakeUser(2, "player");
        var tournament = NewTournament(admin);
        tournament.Participants.Add(new TournamentParticipant
        {
            Tournament = tournament,
            User = player,
            Seed = 1
        });
        Context.Users.AddRange(admin, player);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var listed = await new GetGlobalTournamentsHandler(Context)
            .Handle(new GetGlobalTournamentsQuery(), CancellationToken.None);
        var details = await new GetTournamentHandler(
                Context,
                new GroupPermissionsService(),
                new TournamentDetailsMapperResolver([new SwissTournamentDetailsMapper()]))
            .Handle(new GetTournamentQuery { UserId = 999, TournamentId = tournament.Id }, CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle(t => t.Id == tournament.Id);
        details.IsSuccess.Should().BeTrue();
        details.Value.Swiss!.Standings.Should().ContainSingle(s => s.User.Id == player.Id && s.Points == 0);
    }

    [Fact]
    public async Task Registration_and_leave_are_idempotent_before_start()
    {
        var admin = EntityFactory.MakeUser(1, "admin");
        var player = EntityFactory.MakeUser(2, "player");
        var tournament = NewTournament(admin);
        Context.Users.AddRange(admin, player);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();
        var handler = new ChangeGlobalTournamentRegistrationHandler(Context);
        var join = new ChangeGlobalTournamentRegistrationCommand
        {
            TournamentId = tournament.Id,
            UserId = player.Id,
            Join = true
        };
        var leave = new ChangeGlobalTournamentRegistrationCommand
        {
            TournamentId = tournament.Id,
            UserId = player.Id,
            Join = false
        };

        (await handler.Handle(join, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(join, CancellationToken.None)).IsSuccess.Should().BeTrue();
        tournament.Participants.Should().ContainSingle();
        (await handler.Handle(leave, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(leave, CancellationToken.None)).IsSuccess.Should().BeTrue();
        tournament.Participants.Should().BeEmpty();
        tournament.RegistrationRevision.Should().Be(2);
    }

    [Theory]
    [InlineData(TournamentStatus.New, -1)]
    [InlineData(TournamentStatus.InProgress, 1)]
    public async Task Cannot_change_registration_after_start_boundary(TournamentStatus status, int hoursFromNow)
    {
        var admin = EntityFactory.MakeUser(1, "admin");
        var player = EntityFactory.MakeUser(2, "player");
        var tournament = NewTournament(admin);
        tournament.Status = status;
        tournament.StartsAt = DateTime.UtcNow.AddHours(hoursFromNow);
        Context.Users.AddRange(admin, player);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var result = await new ChangeGlobalTournamentRegistrationHandler(Context)
            .Handle(new ChangeGlobalTournamentRegistrationCommand
            {
                TournamentId = tournament.Id,
                UserId = player.Id,
                Join = true
            }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);
        tournament.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task Concurrent_registration_uses_tournament_revision_to_prevent_lost_update()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connection)
            .Options;
        await using var setup = new Context(options);
        await setup.Database.EnsureCreatedAsync();
        var admin = EntityFactory.MakeUser(1, "admin");
        var player1 = EntityFactory.MakeUser(2, "player1");
        var player2 = EntityFactory.MakeUser(3, "player2");
        var tournament = NewTournament(admin);
        setup.Users.AddRange(admin, player1, player2);
        setup.Tournaments.Add(tournament);
        await setup.SaveChangesAsync();

        await using var first = new Context(options);
        await using var second = new Context(options);
        var firstTournament = await first.Tournaments.OfType<SwissTournament>()
            .Include(t => t.Participants).SingleAsync();
        var secondTournament = await second.Tournaments.OfType<SwissTournament>()
            .Include(t => t.Participants).SingleAsync();
        firstTournament.Participants.Add(new TournamentParticipant
        {
            Tournament = firstTournament,
            User = await first.Users.SingleAsync(u => u.Id == player1.Id),
            Seed = 1
        });
        secondTournament.Participants.Add(new TournamentParticipant
        {
            Tournament = secondTournament,
            User = await second.Users.SingleAsync(u => u.Id == player2.Id),
            Seed = 1
        });
        firstTournament.RegistrationRevision++;
        secondTournament.RegistrationRevision++;

        await first.SaveChangesAsync();
        var losingSave = () => second.SaveChangesAsync();
        await losingSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = new Context(options);
        var savedTournament = await verify.Tournaments.OfType<SwissTournament>()
            .Include(t => t.Participants).ThenInclude(p => p.User).SingleAsync();
        savedTournament.Participants.Should().ContainSingle(p => p.User.Id == player1.Id);
    }

    [Fact]
    public async Task Group_tournament_keeps_cascade_deletion_when_group_is_removed()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Context>().UseSqlite(connection).Options;
        await using (var setup = new Context(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var creator = EntityFactory.MakeUser(1, "creator");
            var group = EntityFactory.MakeGroup(1, "group");
            setup.Tournaments.Add(new SingleEliminationBracketTournament
            {
                Name = "Cup",
                Group = group,
                CreatedBy = creator,
                CreatedAt = DateTime.UtcNow,
                Status = TournamentStatus.New,
                MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
            });
            await setup.SaveChangesAsync();
        }

        await using (var delete = new Context(options))
        {
            delete.Groups.Remove(await delete.Groups.SingleAsync());
            await delete.SaveChangesAsync();
        }

        await using var verify = new Context(options);
        (await verify.Tournaments.CountAsync()).Should().Be(0);
    }

    private static SwissTournament NewTournament(Duely.Domain.Models.User admin) => new()
    {
        Name = "World Cup",
        Description = "Open tournament",
        StartsAt = DateTime.UtcNow.AddHours(1),
        Status = TournamentStatus.New,
        CreatedBy = admin,
        CreatedAt = DateTime.UtcNow,
        MatchmakingType = TournamentMatchmakingType.Swiss
    };
}
