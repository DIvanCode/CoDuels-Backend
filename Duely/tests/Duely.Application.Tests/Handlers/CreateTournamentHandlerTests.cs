using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Tournaments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class CreateTournamentHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creates_tournament_for_group_members()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = creator, Group = group, Role = GroupRole.Creator });
        group.Users.Add(new GroupMembership { User = user1, Group = group, Role = GroupRole.Member });
        group.Users.Add(new GroupMembership { User = user2, Group = group, Role = GroupRole.Member });

        var configuration = new DuelConfiguration
        {
            Id = 77,
            Owner = creator,
            IsRated = false,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] }
            }
        };

        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        Context.DuelConfigurations.Add(configuration);
        await Context.SaveChangesAsync();

        var handler = new CreateTournamentHandler(
            Context,
            new GroupPermissionsService(),
            new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
            {
                new SingleEliminationBracketMatchmakingStrategy()
            }));

        var result = await handler.Handle(new CreateTournamentCommand
        {
            UserId = creator.Id,
            Name = "Spring Cup",
            GroupId = group.Id,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = new List<string> { user1.Nickname, user2.Nickname },
            DuelConfigurationId = configuration.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Spring Cup");
        result.Value.Status.Should().Be(TournamentStatus.New);
        result.Value.Participants.Select(p => p.Nickname).Should().BeEquivalentTo(user1.Nickname, user2.Nickname);

        var tournament = await Context.Tournaments
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .SingleAsync();
        tournament.MatchmakingType.Should().Be(TournamentMatchmakingType.SingleEliminationBracket);
        tournament.Participants.Should().HaveCount(2);
        ((SingleEliminationBracketTournament)tournament).Nodes.Should().HaveCount(3);
    }

    [Fact]
    public async Task Returns_forbidden_for_member_without_permissions()
    {
        var member = EntityFactory.MakeUser(1, "member");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = member, Group = group, Role = GroupRole.Member });
        group.Users.Add(new GroupMembership { User = user1, Group = group, Role = GroupRole.Member });
        group.Users.Add(new GroupMembership { User = user2, Group = group, Role = GroupRole.Member });

        Context.Users.AddRange(member, user1, user2);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CreateTournamentHandler(
            Context,
            new GroupPermissionsService(),
            new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
            {
                new SingleEliminationBracketMatchmakingStrategy()
            }));

        var result = await handler.Handle(new CreateTournamentCommand
        {
            UserId = member.Id,
            Name = "Spring Cup",
            GroupId = group.Id,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = new List<string> { user1.Nickname, user2.Nickname }
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_not_found_for_missing_participant()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = creator, Group = group, Role = GroupRole.Creator });
        group.Users.Add(new GroupMembership { User = user1, Group = group, Role = GroupRole.Member });

        Context.Users.AddRange(creator, user1);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CreateTournamentHandler(
            Context,
            new GroupPermissionsService(),
            new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
            {
                new SingleEliminationBracketMatchmakingStrategy()
            }));

        var result = await handler.Handle(new CreateTournamentCommand
        {
            UserId = creator.Id,
            Name = "Spring Cup",
            GroupId = group.Id,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = new List<string> { user1.Nickname, "ghost" }
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
