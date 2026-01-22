using System.Security.Cryptography;
using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class CreateTicketCommand : IRequest<Result<TicketDto>>
{
    public required int UserId { get; init; }
}

public sealed class CreateTicketHandler(Context context)
    : IRequestHandler<CreateTicketCommand, Result<TicketDto>>
{
    private const int TicketBytesLength = 32;
    private const int TicketGenerationAttempts = 3;

    public async Task<Result<TicketDto>> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        for (var attempt = 0; attempt < TicketGenerationAttempts; attempt++)
        {
            var ticket = GenerateTicket();
            var ticketExists = await context.Users.AnyAsync(u => u.AuthTicket == ticket, cancellationToken);
            if (ticketExists)
            {
                continue;
            }

            user.AuthTicket = ticket;
            await context.SaveChangesAsync(cancellationToken);

            return new TicketDto
            {
                Ticket = ticket
            };
        }

        return Result.Fail("Failed to generate a unique ticket.");
    }

    private static string GenerateTicket()
    {
        var bytes = new byte[TicketBytesLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
