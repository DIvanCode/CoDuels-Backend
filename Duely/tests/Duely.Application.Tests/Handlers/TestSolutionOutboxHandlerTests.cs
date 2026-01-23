using Duely.Application.Services.Outbox.Handlers;
using Duely.Domain.Models;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using FluentResults;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class TestSolutionOutboxHandlerTests
{
    [Fact]
    public async Task HandleAsync_CallsClientWithCorrectParameters()
    {
        var client = new Mock<ITaskiClient>();
        var handler = new TestSolutionHandler(client.Object);

        var payload = new TestSolutionPayload
        {
            TaskId = "TASK-1",
            SubmissionId = 100,
            Solution = "print(1)",
            Language = Language.Python
        };

        client
            .Setup(c => c.TestSolutionAsync("TASK-1", "100", "print(1)", Language.Python, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        client.Verify(c => c.TestSolutionAsync(
            "TASK-1",
            "100",
            "print(1)",
            Language.Python,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFailWhenClientFails()
    {
        var client = new Mock<ITaskiClient>();
        var handler = new TestSolutionHandler(client.Object);

        var payload = new TestSolutionPayload
        {
            TaskId = "TASK-1",
            SubmissionId = 100,
            Solution = "print(1)",
            Language = Language.Python
        };

        client
            .Setup(c => c.TestSolutionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Language>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Client error"));

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Client error");
    }
}
