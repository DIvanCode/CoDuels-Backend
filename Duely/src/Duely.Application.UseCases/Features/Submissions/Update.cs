using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Application.UseCases.Errors;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class UpdateSubmissionStatusCommand : IRequest<Result>
{
    public required int SubmissionId { get; init; }
    public required string Type { get; init; }
    public string? Verdict { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateSubmissionStatusHandler(Context context) : IRequestHandler<UpdateSubmissionStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSubmissionStatusCommand command, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions.SingleOrDefaultAsync(s => s.Id == command.SubmissionId, cancellationToken);
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
        }
        if (!string.IsNullOrEmpty(command.Message))
        {
            submission.Message = command.Message;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
