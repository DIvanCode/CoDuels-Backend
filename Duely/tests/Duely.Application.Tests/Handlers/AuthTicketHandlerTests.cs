using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class AuthTicketHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task CreateAuthTicket_returns_not_found_when_user_missing()
    {
        var handler = new CreateTicketHandler(Context);

        var res = await handler.Handle(
            new CreateTicketCommand { UserId = 777 },
            CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task CreateAuthTicket_persists_and_returns_ticket()
    {
        var user = new User
        {
            Id = 1,
            Nickname = "u1",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = 0,
            CreatedAt = DateTime.UtcNow
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new CreateTicketHandler(Context);

        var res = await handler.Handle(
            new CreateTicketCommand { UserId = user.Id },
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Ticket.Should().NotBeNullOrWhiteSpace();

        var stored = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        stored.AuthTicket.Should().Be(res.Value.Ticket);
    }

    [Fact]
    public async Task ConsumeAuthTicket_returns_auth_error_when_ticket_missing()
    {
        var handler = new GetUserByTicketHandler(Context);

        var res = await handler.Handle(
            new GetUserByTicketCommand { Ticket = "" },
            CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is AuthenticationError);
    }

    [Fact]
    public async Task ConsumeAuthTicket_returns_auth_error_when_ticket_not_found()
    {
        var handler = new GetUserByTicketHandler(Context);

        var res = await handler.Handle(
            new GetUserByTicketCommand { Ticket = "missing" },
            CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is AuthenticationError);
    }

    [Fact]
    public async Task ConsumeAuthTicket_clears_ticket_and_returns_user_id()
    {
        var user = new User
        {
            Id = 2,
            Nickname = "u2",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            AuthTicket = "ticket",
            Rating = 0,
            CreatedAt = DateTime.UtcNow
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new GetUserByTicketHandler(Context);

        var res = await handler.Handle(
            new GetUserByTicketCommand { Ticket = "ticket" },
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().Be(user.Id);

        var stored = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        stored.AuthTicket.Should().BeNull();
    }
}
