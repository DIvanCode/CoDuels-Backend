using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts; 
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Submissions;

public sealed class SendSubmissionHandler(Context context, ITaskiClient taskiClient) : IRequestHandler<SendSubmissionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SendSubmissionCommand request, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .FirstOrDefaultAsync(d => d.Id == request.DuelId, cancellationToken);
        if (duel is null){
            return Result.Fail<int>($"Duel {request.DuelId} not found");
        }
        var submission = new Submission
        {
            Duel = duel,
            UserId = request.UserId,
            Code = request.Code,
            Language = request.Language,
            SubmitTime = DateTime.Now,
            Status = SubmissionStatus.Queued,
            Verdict = null
        };
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken); 
        var sendResult = await taskiClient.SendSubmission(
            duel.TaskId,
            submission.Id,
            submission.Code,
            submission.Language
        );
        if (!sendResult.IsSuccess)
        {
            return Result.Fail<int>($"Failed to send submission {submission.Id} for task {duel.TaskId}");
        }
        return Result.Ok(submission.Id);
    }
}
