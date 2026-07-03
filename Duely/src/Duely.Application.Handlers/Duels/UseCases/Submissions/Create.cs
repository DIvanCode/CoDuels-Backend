using Duely.Application.Handlers.Duels.Models;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.UseCases.Submissions;

public sealed class CreateSubmissionCommand : IRequest<Result<SubmissionDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
    public required int ProblemPosition { get; init; }
    public required string Source { get; init; }
    public required Language Language { get; init; }
}

internal sealed class CreateSubmissionHandler(
    Context context,
    ILogger<CreateSubmissionHandler> logger)
    : IRequestHandler<CreateSubmissionCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(CreateSubmissionCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var duel = await context.Duels
            .Where(d => d.Id == command.DuelId)
            .Include(d => d.Participants)
            .ThenInclude(p => p.User)
            .Include(d => d.Problems)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new DuelNotFoundError();
        }

        if (duel.Participants.All(p => p.User.Id != command.UserId))
        {
            return new ForbiddenError();
        }
        
        var duelProblem = duel.Problems.SingleOrDefault(p => p.Position == command.ProblemPosition);
        if (duelProblem is null)
        {
            return new NotFoundError("Задача с такой позицией в дуэли не найдена.");
        }

        if (!duelProblem.IsVisible)
        {
            return new ForbiddenError("Задача пока недоступна для решения.");
        }

        var submission = Submission.Create(user, duelProblem, command.Source, command.Language);
        
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} submitted solution to problem {Position} in duel {DuelId}",
            user.Nickname, duelProblem.Position, duel.Id);

        return new SubmissionDto
        {
            Id = submission.Id,
            DuelId = duel.Id,
            ProblemPosition = duelProblem.Position,
            Source = submission.Source,
            Language = submission.Language
        };
    }
}

internal sealed class CreateSubmissionCommandValidator : AbstractValidator<CreateSubmissionCommand>
{
    public CreateSubmissionCommandValidator()
    {
        RuleFor(c => c.Source)
            .MaximumLength(DuelConstants.Submission.MaxLength)
            .WithMessage($"Решение не может содержать более {DuelConstants.Submission.MaxLength} символов.");
    }
}
