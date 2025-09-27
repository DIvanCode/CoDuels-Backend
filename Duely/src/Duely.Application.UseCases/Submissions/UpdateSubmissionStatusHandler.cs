using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Submissions;

public sealed class UpdateSubmissionStatusHandler(Context context)
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
        Console.WriteLine($"Посылка {submission.Id} для пользователя {submission.UserId} сейчас {submission.Status} ({submission.Verdict})");
        return Result.Ok();
    }
}