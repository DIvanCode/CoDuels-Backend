using Duely.Application.Handlers.Users.Validators;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Users.UseCases;

public sealed class SetIdentityTicketCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string IdentityTicket { get; init; }
}

internal sealed class SetIdentityTicketHandler(Context context, ILogger<SetIdentityTicketHandler> logger)
    : IRequestHandler<SetIdentityTicketCommand, Result>
{
    public async Task<Result> Handle(SetIdentityTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new UserNotFoundError();
        }
        
        var userWithIdentityTicketExists = await context.Users
            .Where(u => u.IdentityTicket == command.IdentityTicket)
            .AnyAsync(cancellationToken);
        if (userWithIdentityTicketExists)
        {
            return new InvalidOperationError("Пользователь с заданным идентификационным билетом уже существует.");
        }

        user.SetIdentityTicket(command.IdentityTicket);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} set identity ticket", user.Nickname);

        return Result.Ok();
    }
}

internal sealed class SetIdentityTicketCommandValidator : AbstractValidator<SetIdentityTicketCommand>
{
    public SetIdentityTicketCommandValidator(IdentityTicketValidator identityTicketValidator)
    {
        RuleFor(x => x.IdentityTicket).SetValidator(identityTicketValidator);
    }
}
