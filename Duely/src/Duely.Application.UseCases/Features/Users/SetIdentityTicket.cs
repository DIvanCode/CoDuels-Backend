using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class SetIdentityTicketCommand : IRequest<Result>
{
    public required Guid UserId { get; init; }
    public required string IdentityTicket { get; init; }
}

internal sealed class SetIdentityTicketHandler(
    Context context,
    ILogger<SetIdentityTicketHandler> logger)
    : IRequestHandler<SetIdentityTicketCommand, Result>
{
    public async Task<Result> Handle(SetIdentityTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new UserNotFoundError();
        }
        
        var userWithIdentityTicketExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.IdentityTicket == command.IdentityTicket, cancellationToken);
        if (userWithIdentityTicketExists)
        {
            return new UnexpectedError("Пользователь с заданным идентификационным билетом уже существует.");
        }

        user.SetIdentityTicket(command.IdentityTicket);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} set identity ticket", user.Nickname);

        return Result.Ok();
    }
}

internal sealed class SetIdentityTicketCommandValidator : AbstractValidator<SetIdentityTicketCommand>
{
    public SetIdentityTicketCommandValidator()
    {
        RuleFor(x => x.IdentityTicket)
            .MaximumLength(1024).WithMessage("Слишком длинный идентификационный билет.");
    }
}
