using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class UseIdentityTicketCommand : IRequest<Result<UserDto>>
{
    public required string IdentityTicket { get; init; }
}

public sealed class UseIdentityTicketHandler(Context context, ILogger<UseIdentityTicketHandler> logger)
    : IRequestHandler<UseIdentityTicketCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UseIdentityTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .SingleOrDefaultAsync(u => u.IdentityTicket == command.IdentityTicket, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        user.ClearIdentityTicket();
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} used identity ticket", user.Nickname);

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname.Value,
            Rating = user.Rating.Value,
            CreatedAt = user.CreatedAt
        };
    }
}
