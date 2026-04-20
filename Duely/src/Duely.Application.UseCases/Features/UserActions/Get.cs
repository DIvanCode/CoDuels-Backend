using Duely.Domain.Models.UserActions;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.UserActions;

public sealed record GetUserActionsQuery : IRequest<Result<List<UserAction>>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required char TaskKey { get; init; }
}

public sealed class GetUserActionsHandler(Context context)
    : IRequestHandler<GetUserActionsQuery, Result<List<UserAction>>>
{
    public async Task<Result<List<UserAction>>> Handle(GetUserActionsQuery query, CancellationToken cancellationToken)
    {
        var actions = await context.UserActions
            .AsNoTracking()
            .Where(action =>
                action.DuelId == query.DuelId &&
                action.UserId == query.UserId &&
                action.TaskKey == query.TaskKey)
            .OrderBy(action => action.SequenceId)
            .ToListAsync(cancellationToken);

        return actions;
    }
}

public sealed class GetUserActionsQueryValidator : AbstractValidator<GetUserActionsQuery>
{
    public GetUserActionsQueryValidator()
    {
        RuleFor(x => x.DuelId)
            .GreaterThan(0)
            .WithMessage("Duel id is required.");

        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User id is required.");

        RuleFor(x => x.TaskKey)
            .NotEqual(default(char))
            .WithMessage("Task key is required.");
    }
}
