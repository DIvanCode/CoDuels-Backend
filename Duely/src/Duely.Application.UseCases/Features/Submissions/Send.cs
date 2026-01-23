using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Application.UseCases.Dtos;
using System.Text.Json;
using Duely.Application.Services.Errors;
using Duely.Application.Services.RateLimiting;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Microsoft.Extensions.Logging;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed record SendSubmissionCommand : IRequest<Result<SubmissionDto>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required char TaskKey { get; init; }
    public required string Solution { get; init; }
    public required Language Language { get; init; }
}

public sealed class SendSubmissionHandler(
    Context context,
    ISubmissionRateLimiter submissionLimiter,
    ITaskService taskService)
    : IRequestHandler<SendSubmissionCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(SendSubmissionCommand command, CancellationToken cancellationToken)
    {
        if (await submissionLimiter.IsLimitExceededAsync(command.UserId, cancellationToken))
        {
            return new RateLimitExceededError("Too many submissions.");
        }

        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        
        var duel = await context.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Configuration)
            .Include(d => d.Submissions)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (duel.User1.Id != command.UserId && duel.User2.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Duel), "send submission to", nameof(Duel.Id), command.DuelId);
        }

        if (!duel.Tasks.TryGetValue(command.TaskKey, out _))
        {
            return new EntityNotFoundError(nameof(DuelTask), "key", command.TaskKey);
        }

        if (!taskService.IsTaskVisible(duel, command.UserId, command.TaskKey))
        {
            return new ForbiddenError(nameof(DuelTask), "submit", "key", command.TaskKey);
        }
        
        var isUpsolving = duel.Status == DuelStatus.Finished;
        var taskId = duel.Tasks[command.TaskKey].Id;
        var submission = new Submission
        {
            Duel = duel,
            User = user,
            TaskKey = command.TaskKey,
            Solution = command.Solution,
            Language = command.Language,
            SubmitTime = DateTime.UtcNow,
            Status = SubmissionStatus.Queued,
            IsUpsolving = isUpsolving
        };
        
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);
        
        var retryUntil = duel.DeadlineTime > DateTime.UtcNow
            ? duel.DeadlineTime.AddMinutes(5)
            : DateTime.UtcNow.AddMinutes(5);
        
        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.TestSolution,
            Payload = new TestSolutionPayload
            {
                TaskId = taskId,
                SubmissionId = submission.Id,
                Solution = submission.Solution,
                Language = submission.Language
            },
            RetryUntil = retryUntil
        });
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
            
        return new SubmissionDto
        {
            SubmissionId = submission.Id,
            Solution = submission.Solution,
            Language = submission.Language,
            Status = submission.Status,
            CreatedAt = submission.SubmitTime,
            Message = submission.Message,
            Verdict = submission.Verdict,
            IsUpsolving = submission.IsUpsolving
        };
    }
}
