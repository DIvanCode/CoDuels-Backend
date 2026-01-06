using Duely.Application.Services.Outbox.Handlers;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using FluentResults;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class TestSolutionOutboxHandlerTests
{
    [Fact]
    public void Type_ReturnsTestSolution()
    {
        var client = new Mock<ITaskiClient>();
        var handler = new TestSolutionHandler(client.Object);

        handler.Type.Should().Be(OutboxType.TestSolution);
    }

    [Fact]
    public async Task HandleAsync_CallsClientWithCorrectParameters()
    {
        var client = new Mock<ITaskiClient>();
        var handler = new TestSolutionHandler(client.Object);

        var payload = new TestSolutionPayload("TASK-1", 100, "print(1)", "py");

        client
            .Setup(c => c.TestSolutionAsync("TASK-1", "100", "print(1)", "py", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        client.Verify(c => c.TestSolutionAsync(
            "TASK-1",
            "100",
            "print(1)",
            "py",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFailWhenClientFails()
    {
        var client = new Mock<ITaskiClient>();
        var handler = new TestSolutionHandler(client.Object);

        var payload = new TestSolutionPayload("TASK-1", 100, "print(1)", "py");

        client
            .Setup(c => c.TestSolutionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Client error"));

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Client error");
    }
}