using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using MediatR;
using FluentResults;
using Duely.Infrastructure.Gateway.Client.Abstracts.Messages;
namespace Duely.Application.UseCases.Submissions;

public sealed class UpdateSubmissionStatusHandler(Context context,IMessageSender messageSender)
    : IRequestHandler<UpdateSubmissionStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSubmissionStatusCommand request, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission is null)
            return Result.Fail($"Submission {request.SubmissionId} not found");
        if (request.Type == "start")
        {
            submission.Status = SubmissionStatus.Running;
            await context.SaveChangesAsync(cancellationToken);
            var message = new SubmissionUpdateMessage
            {
                SubmissionId = submission.Id,
                Status = "Running"
            };
            await messageSender.SendMessage(message, cancellationToken);
        }
        else if (request.Type == "status")
        {
            submission.Status = SubmissionStatus.Running;
            await context.SaveChangesAsync(cancellationToken);
            var message = new SubmissionUpdateMessage
            {
                SubmissionId = submission.Id,
                Status = "Running",
                StatusText = request.Message
            };

            await messageSender.SendMessage(message, cancellationToken);
        }
        else if (request.Type == "finish")
        {
            submission.Status = SubmissionStatus.Done;

            if (!string.IsNullOrEmpty(request.Verdict))
            {
                submission.Verdict = request.Verdict;
            }
            else if (!string.IsNullOrEmpty(request.Error))
            {
                submission.Verdict = "Technical error";
            }
            await context.SaveChangesAsync(cancellationToken);

            var message = new SubmissionVerdictMessage
            {
                SubmissionId = submission.Id,
                Verdict = submission.Verdict!,
                Error = request.Error
            };

            await messageSender.SendMessage(message, cancellationToken);
        }

        return Result.Ok();

    }
}