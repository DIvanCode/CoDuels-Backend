using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.UserActions;
using Duely.Application.Services.Errors;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.UserActions;

public sealed record SaveUserActionsCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required IReadOnlyList<UserAction> Actions { get; init; }
}

public sealed class SaveUserActionsHandler(Context context)
    : IRequestHandler<SaveUserActionsCommand, Result>
{
    public async Task<Result> Handle(SaveUserActionsCommand command, CancellationToken cancellationToken)
    {
        if (command.Actions.Any(userAction => userAction.UserId != command.UserId))
        {
            return new ForbiddenError("Forbidden to save actions for another user.");
        }

        var duelIds = command.Actions
            .Select(action => action.DuelId)
            .Distinct()
            .ToArray();

        var availableDuelIds = await context.Duels
            .AsNoTracking()
            .Where(duel => duelIds.Contains(duel.Id) && duel.Status != DuelStatus.Finished)
            .Select(duel => duel.Id)
            .ToListAsync(cancellationToken);

        var availableDuelIdsSet = availableDuelIds.ToHashSet();
        var actionsToSave = command.Actions
            .Where(action => availableDuelIdsSet.Contains(action.DuelId))
            .ToList();

        if (actionsToSave.Count == 0)
        {
            return Result.Ok();
        }

        context.UserActions.AddRange(actionsToSave);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

public sealed class SaveUserActionsCommandValidator : AbstractValidator<SaveUserActionsCommand>
{
    public SaveUserActionsCommandValidator()
    {
        RuleFor(x => x.Actions)
            .NotEmpty()
            .WithMessage("Actions list is required.");

        RuleForEach(x => x.Actions)
            .NotNull()
            .WithMessage("Action cannot be null.");

        RuleForEach(x => x.Actions)
            .SetValidator(new UserActionValidator());
    }
}

public sealed class UserActionValidator : AbstractValidator<UserAction>
{
    public UserActionValidator()
    {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event id is required.");

        RuleFor(x => x.SequenceId)
            .GreaterThan(0)
            .WithMessage("Sequence id must be greater than 0.");

        RuleFor(x => x.Timestamp)
            .NotEqual(default(DateTime))
            .WithMessage("Timestamp is required.");

        RuleFor(x => x.DuelId)
            .GreaterThan(0)
            .WithMessage("Duel id is required.");

        RuleFor(x => x.TaskKey)
            .NotEqual(default(char))
            .WithMessage("Task key is required.");
    }
}
