using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Features.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using System.Text.Json;
using Duely.Application.UseCases.Payloads;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed record SendSubmissionCommand : IRequest<Result<SubmissionDto>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
}

public sealed class SendSubmissionHandler(Context context, ISubmissionRateLimiter submissionLimiter, ILogger<SendSubmissionHandler> logger)
    : IRequestHandler<SendSubmissionCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(SendSubmissionCommand command, CancellationToken cancellationToken)
    {
        if (await submissionLimiter.IsLimitExceededAsync(command.UserId, cancellationToken))
        {
            logger.LogWarning("Submission rate limit exceeded. UserId = {UserId}, DuelId = {DuelId}",
                command.UserId, command.DuelId
            );
            return new RateLimitExceededError("Too many submissions.");
        }

        var duel = await context.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
            
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (duel.User1.Id != command.UserId && duel.User2.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Duel), "send submission to", nameof(Duel.Id), command.DuelId);
        }
        
        var retryUntil = duel.DeadlineTime > DateTime.UtcNow ? duel.DeadlineTime.AddMinutes(5) : DateTime.UtcNow.AddMinutes(5);
        
        var isUpsolve = duel.Status == DuelStatus.Finished;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var submission = new Submission
            {
                Duel = duel,
                User = user,
                Code = command.Code,
                Language = command.Language,
                SubmitTime = DateTime.UtcNow,
                Status = SubmissionStatus.Queued,
                IsUpsolve = isUpsolve
            };

            context.Submissions.Add(submission);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Submission created. SubmissionId = {SubmissionId}, DuelId = {DuelId}, UserId = {UserId}, Language = {Language}, IsUpsolve = {IsUpsolve}",
                submission.Id, duel.Id, user.Id, submission.Language, submission.IsUpsolve
            );


            var payload = JsonSerializer.Serialize(new TestSolutionPayload(duel.TaskId, submission.Id, submission.Code, submission.Language));

            context.Outbox.Add(new OutboxMessage
            {
                Type = OutboxType.TestSolution,
                Status = OutboxStatus.ToDo,
                Retries = 0,
                RetryAt = null,
                Payload = payload,
                RetryUntil = retryUntil
            });

            logger.LogDebug("Submission test queued via outbox. SubmissionId={SubmissionId} RetryUntil={RetryUntil}",
                submission.Id, retryUntil
            );


            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SubmissionDto
            {
                SubmissionId = submission.Id,
                Solution = submission.Code,
                Language = submission.Language,
                Status = submission.Status,
                CreatedAt = submission.SubmitTime,
                Message = submission.Message,
                Verdict = submission.Verdict,
                IsUpsolve = submission.IsUpsolve
            };
        }
        catch
        {
            logger.LogError("Submission transaction failed. UserId = {UserId}, DuelId = {DuelId}",
                command.UserId, command.DuelId
            );
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
