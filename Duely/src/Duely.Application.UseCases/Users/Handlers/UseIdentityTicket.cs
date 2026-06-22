using Duely.Application.UseCases.Users.Models;
using Duely.Application.UseCases.Users.Validators;
using Duely.Domain.Kernel.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Users.Handlers;

public sealed class UseIdentityTicketCommand : IRequest<Result<UserDto>>
{
    public required string IdentityTicket { get; init; }
}

internal sealed class UseIdentityTicketHandler(Context context, ILogger<UseIdentityTicketHandler> logger)
    : IRequestHandler<UseIdentityTicketCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UseIdentityTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.IdentityTicket == command.IdentityTicket)
            .SingleOrDefaultAsync(cancellationToken);
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
            Nickname = user.Nickname,
            CreatedAt = user.CreatedAt,
            Rating = user.Rating
        };
    }
}

internal sealed class UseIdentityTicketCommandValidator : AbstractValidator<UseIdentityTicketCommand>
{
    public UseIdentityTicketCommandValidator(IdentityTicketValidator identityTicketValidator)
    {
        RuleFor(x => x.IdentityTicket).SetValidator(identityTicketValidator);
    }
}
