using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Application.UseCases.Errors;
using Microsoft.Extensions.Logging;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models.Messages;
using System.Text.Json;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class UpdateSubmissionStatusCommand : IRequest<Result>
{
    public required int SubmissionId { get; init; }
    public required string Type { get; init; }
    public string? Verdict { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateSubmissionStatusHandler(Context context, ILogger<UpdateSubmissionStatusHandler> logger)
    : IRequestHandler<UpdateSubmissionStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSubmissionStatusCommand command, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .Include(s => s.Duel)
            .ThenInclude(d => d.User1)
            .Include(s => s.Duel)
            .ThenInclude(d => d.User2)
            .SingleOrDefaultAsync(s => s.Id == command.SubmissionId, cancellationToken);
        if (submission is null)
        {
            logger.LogWarning("UpdateSubmissionStatusHandler failed: Submission {SubmissionId} not found", command.SubmissionId);
            return new EntityNotFoundError(nameof(Submission), nameof(Submission.Id), command.SubmissionId);
        }

        if (submission.Status == SubmissionStatus.Done)
        {
            return Result.Ok();
        }

        if (command.Type is "start" or "status")
        {
            submission.Status = SubmissionStatus.Running;
        }

        if (!string.IsNullOrEmpty(command.Error))
        {
            submission.Status = SubmissionStatus.Done;
            submission.Verdict = "Technical error";
            submission.Message = null;
        }
        if (!string.IsNullOrEmpty(command.Verdict))
        {
            submission.Status = SubmissionStatus.Done;
            submission.Verdict = command.Verdict;
            submission.Message = null;

            logger.LogInformation("Testing submission is completed. SubmissionId = {SubmissionId}, Verdict = {Verdict}",
                submission.Id, command.Verdict
            );

            if (command.Verdict == "Accepted")
            {
                var retryUntil = submission.Duel.DeadlineTime;
                var payload1 = JsonSerializer.Serialize(
                    new SendMessagePayload(submission.Duel.User1.Id, MessageType.DuelChanged, submission.Duel.Id));
                var payload2 = JsonSerializer.Serialize(
                    new SendMessagePayload(submission.Duel.User2.Id, MessageType.DuelChanged, submission.Duel.Id));

                context.Outbox.Add(new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = payload1,
                    Status = OutboxStatus.ToDo,
                    Retries = 0,
                    RetryAt = null,
                    RetryUntil = retryUntil
                });

                context.Outbox.Add(new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = payload2,
                    Status = OutboxStatus.ToDo,
                    Retries = 0,
                    RetryAt = null,
                    RetryUntil = retryUntil
                });
            }
        }
        if (!string.IsNullOrEmpty(command.Message))
        {
            submission.Message = command.Message;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
