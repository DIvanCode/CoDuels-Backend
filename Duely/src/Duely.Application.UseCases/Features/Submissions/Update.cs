using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Domain.Models.Messages;
using Duely.Application.Services.Errors;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class UpdateSubmissionStatusCommand : IRequest<Result>
{
    public required int SubmissionId { get; init; }
    public required string Type { get; init; }
    public string? Verdict { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateSubmissionStatusHandler(Context context)
    : IRequestHandler<UpdateSubmissionStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSubmissionStatusCommand command, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .Include(s => s.User)
            .Include(s => s.Duel)
            .ThenInclude(d => d.User1)
            .Include(s => s.Duel)
            .ThenInclude(d => d.User2)
            .SingleOrDefaultAsync(s => s.Id == command.SubmissionId, cancellationToken);
        if (submission is null)
        {
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

            if (command.Verdict == "Accepted")
            {
                var retryUntil = submission.Duel.DeadlineTime;

                context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = new SendMessagePayload
                    {
                        UserId = submission.Duel.User1.Id,
                        Message = new DuelChangedMessage
                        {
                            DuelId = submission.Duel.Id
                        }
                    },
                    RetryUntil = retryUntil
                });

                context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = new SendMessagePayload
                    {
                        UserId = submission.Duel.User2.Id,
                        Message = new DuelChangedMessage
                        {
                            DuelId = submission.Duel.Id
                        }
                    },
                    RetryUntil = retryUntil
                });
            }
        }
        if (!string.IsNullOrEmpty(command.Message))
        {
            submission.Message = command.Message;
        }

        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = submission.User.Id,
                Message = new SubmissionStatusUpdatedMessage
                {
                    DuelId = submission.Duel.Id,
                    SubmissionId = submission.Id,
                    Status = submission.Status,
                    Message = submission.Message,
                    Verdict = submission.Verdict
                }
            },
            RetryUntil = DateTime.UtcNow.AddSeconds(10)
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
