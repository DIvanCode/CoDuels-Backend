using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts; 
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed record SendSubmissionCommand : IRequest<Result<SubmissionDto>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
}

public sealed class SendSubmissionHandler(Context context, ITaskiClient taskiClient)
    : IRequestHandler<SendSubmissionCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(SendSubmissionCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels.SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var submission = new Submission
        {
            Duel = duel,
            User = user,
            Code = command.Code,
            Language = command.Language,
            SubmitTime = DateTime.Now,
            Status = SubmissionStatus.Queued
        };

        context.Submissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);
        
        var sendResult = await taskiClient.TestSolutionAsync(
            duel.TaskId,
            submission.Id.ToString(),
            submission.Code,
            submission.Language,
            cancellationToken);
        if (sendResult.IsFailed)
        {
            return sendResult;
        }

        return new SubmissionDto
        {
            SubmissionId = submission.Id,
            Solution = submission.Code,
            Language = submission.Language,
            Status = submission.Status,
            SubmitTime = submission.SubmitTime,
            Message = submission.Message,
            Verdict = submission.Verdict
        };
    }
}
