using Duely.Application.UseCases.Features.Users;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Validators;

public class GetUserByTicketCommandValidatorTests
{
    [Fact]
    public void Rejects_empty_ticket()
    {
        var validator = new GetUserByTicketCommandValidator();

        var result = validator.Validate(new GetUserByTicketCommand { Ticket = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Ticket");
    }

    [Fact]
    public void Accepts_non_empty_ticket()
    {
        var validator = new GetUserByTicketCommandValidator();

        var result = validator.Validate(new GetUserByTicketCommand { Ticket = "ticket-123" });

        result.IsValid.Should().BeTrue();
    }
}
