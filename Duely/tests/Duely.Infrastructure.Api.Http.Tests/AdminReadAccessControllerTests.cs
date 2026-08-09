using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Groups;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.Api.Http.Controllers;
using Duely.Infrastructure.Api.Http.Services;
using FluentResults;
using MediatR;
using Moq;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class AdminReadAccessControllerTests
{
    [Fact]
    public async Task Duel_details_pass_admin_claim_to_query()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetDuelQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<DuelDto>(null!));
        var userContext = CreateAdminContext();
        var controller = new DuelsController(mediator.Object, userContext.Object, Mock.Of<IRatingManager>());

        await controller.GetAsync(10, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetDuelQuery>(q => q.UserId == 42 && q.DuelId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Submission_reads_pass_admin_claim_to_queries()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetUserSubmissionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new List<SubmissionListItemDto>()));
        mediator
            .Setup(m => m.Send(It.IsAny<GetSubmissionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<SubmissionDto>(null!));
        var userContext = CreateAdminContext();
        var controller = new SubmissionsController(mediator.Object, userContext.Object);

        await controller.GetUserSubmissionsAsync(10, 'A', CancellationToken.None);
        await controller.GetSubmissionAsync(10, 20, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetUserSubmissionsQuery>(q =>
                q.UserId == 42 && q.DuelId == 10 && q.TaskKey == 'A' && q.IsAdmin),
            It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(
            It.Is<GetSubmissionQuery>(q =>
                q.UserId == 42 && q.DuelId == 10 && q.SubmissionId == 20 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Group_reads_pass_admin_claim_to_every_query()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetGroupQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<GroupDto>(null!));
        mediator
            .Setup(m => m.Send(It.IsAny<GetGroupUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new List<GroupUserDto>()));
        mediator
            .Setup(m => m.Send(It.IsAny<GetGroupDuelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new List<GroupDuelDto>()));
        mediator
            .Setup(m => m.Send(It.IsAny<GetGroupTournamentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new List<TournamentDto>()));
        var userContext = CreateAdminContext();
        var controller = new GroupsController(mediator.Object, userContext.Object);

        await controller.GetAsync(10, CancellationToken.None);
        await controller.GetUsersAsync(10, CancellationToken.None);
        await controller.GetGroupDuelsAsync(10, CancellationToken.None);
        await controller.GetGroupTournamentsAsync(10, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetGroupQuery>(q => q.UserId == 42 && q.GroupId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(
            It.Is<GetGroupUsersQuery>(q => q.UserId == 42 && q.GroupId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(
            It.Is<GetGroupDuelsQuery>(q => q.UserId == 42 && q.GroupId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(
            It.Is<GetGroupTournamentsQuery>(q => q.UserId == 42 && q.GroupId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Tournament_details_pass_admin_claim_to_query()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetTournamentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<TournamentDetailsDto>(null!));
        var userContext = CreateAdminContext();
        var controller = new TournamentsController(mediator.Object, userContext.Object);

        await controller.GetAsync(10, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetTournamentQuery>(q => q.UserId == 42 && q.TournamentId == 10 && q.IsAdmin),
            It.IsAny<CancellationToken>()));
    }

    private static Mock<IUserContext> CreateAdminContext()
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(context => context.UserId).Returns(42);
        userContext.Setup(context => context.IsAdmin()).Returns(true);
        return userContext;
    }
}
