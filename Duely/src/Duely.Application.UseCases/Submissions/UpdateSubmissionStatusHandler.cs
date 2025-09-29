using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Submissions;

public sealed class UpdateSubmissionStatusHandler(Context context,IMessageSender messageSender)
    : IRequestHandler<UpdateSubmissionStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSubmissionStatusCommand request, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId,cancellationToken);
            
        if (submission is null)
            return Result.Fail($"Submission {request.SubmissionId} not found");

        submission.Status = request.Status;
        submission.Verdict = request.Verdict;

        await context.SaveChangesAsync(cancellationToken);
        var message = new SubmissionUpdateMessage
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            Verdict = submission.Verdict
        };
        
        await messageSender.SendMessage(message, cancellationToken);
        return Result.Ok();
    }
}