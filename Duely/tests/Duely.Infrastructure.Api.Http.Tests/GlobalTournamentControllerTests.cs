using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Tournaments;
using Duely.Infrastructure.Api.Http.Controllers;
using Duely.Infrastructure.Api.Http.Requests.Tournaments;
using Duely.Infrastructure.Api.Http.Services;
using FluentResults;
using MediatR;
using Moq;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class GlobalTournamentControllerTests
{
    [Fact]
    public async Task Creation_passes_global_fields_and_admin_claim_to_handler()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateTournamentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<TournamentDto>(null!));
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(c => c.UserId).Returns(42);
        userContext.Setup(c => c.IsAdmin()).Returns(true);
        var start = DateTime.UtcNow.AddHours(1);
        var controller = new TournamentsController(mediator.Object, userContext.Object);

        await controller.CreateAsync(new CreateTournamentRequest
        {
            Name = "World Cup",
            MatchmakingType = TournamentMatchmakingType.Swiss,
            Description = "Open tournament",
            StartsAt = start,
            DuelConfigurationId = 11
        }, CancellationToken.None);

        mediator.Verify(m => m.Send(It.Is<CreateTournamentCommand>(c =>
            c.UserId == 42 && c.IsAdmin && c.GroupId == null &&
            c.Description == "Open tournament" && c.StartsAt == start &&
            c.MatchmakingType == TournamentMatchmakingType.Swiss &&
            c.DuelConfigurationId == 11 && c.Participants.Count == 0),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Registration_endpoints_pass_caller_identity_and_requested_action()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.IsAny<ChangeGlobalTournamentRegistrationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<TournamentDto>(null!));
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(c => c.UserId).Returns(42);
        var controller = new TournamentsController(mediator.Object, userContext.Object);

        await controller.RegisterAsync(10, CancellationToken.None);
        await controller.LeaveAsync(10, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<ChangeGlobalTournamentRegistrationCommand>(c =>
                c.UserId == 42 && c.TournamentId == 10 && c.Join),
            It.IsAny<CancellationToken>()));
        mediator.Verify(m => m.Send(
            It.Is<ChangeGlobalTournamentRegistrationCommand>(c =>
                c.UserId == 42 && c.TournamentId == 10 && !c.Join),
            It.IsAny<CancellationToken>()));
    }
}
