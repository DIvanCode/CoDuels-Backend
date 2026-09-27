using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duely.Application.Tests.Handlers;

public class RegisterHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task AlreadyExists_when_nickname_dup()
    {
        var ctx = Context;

        ctx.Users.Add(new User { Id = 1, Nickname = "alice", PasswordHash = "h", PasswordSalt = "s", Rating = 0, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var handler = new RegisterHandler(ctx, NullLogger<RegisterHandler>.Instance);

        var password = $"test-only-{Guid.NewGuid():N}";

        var res = await handler.Handle(new RegisterCommand
        {
            Nickname = "alice",
            Password = password
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task Success_creates_user_with_hash_and_salt()
    {
        var ctx = Context;

        var handler = new RegisterHandler(ctx, NullLogger<RegisterHandler>.Instance);

        var password = $"test-only-{Guid.NewGuid():N}";

        var res = await handler.Handle(new RegisterCommand
        {
            Nickname = "bob",
            Password = password
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var user = await ctx.Users.SingleAsync(u => u.Nickname == "bob");
        user.PasswordSalt.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBeNullOrWhiteSpace();

        BCrypt.Net.BCrypt.Verify(password + user.PasswordSalt, user.PasswordHash).Should().BeTrue();
    }
}