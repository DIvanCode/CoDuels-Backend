using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class GetUserByTicketCommand : IRequest<Result<int>>
{
    public required string Ticket { get; init; }
}

public sealed class GetUserByTicketHandler(Context context) : IRequestHandler<GetUserByTicketCommand, Result<int>>
{
    public async Task<Result<int>> Handle(GetUserByTicketCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.AuthTicket == command.Ticket, cancellationToken);
        if (user is null)
        {
            return new AuthenticationError();
        }

        user.AuthTicket = null;
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

public class GetUserByTicketCommandValidator : AbstractValidator<GetUserByTicketCommand>
{
    public GetUserByTicketCommandValidator()
    {
        RuleFor(x => x.Ticket).NotEmpty().WithMessage("Ticket must be not empty.");
    }
}
