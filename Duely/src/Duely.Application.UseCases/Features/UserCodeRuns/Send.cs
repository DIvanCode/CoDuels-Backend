using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.UserCodeRuns;

public sealed class RunUserCodeCommand : IRequest<Result<UserCodeRunDto>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
    public required string Input { get; init; }
}

public sealed class RunUserCodeHandler(Context context) : IRequestHandler<RunUserCodeCommand, Result<UserCodeRunDto>>
{
    public async Task<Result<UserCodeRunDto>> Handle(RunUserCodeCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        var user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var run = new UserCodeRun
        {
            Duel = duel,
            User = user,
            Code = command.Code,
            Language = command.Language,
            Input = command.Input,
            CreatedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Queued,
            Verdict = null,
            Output = null,
            Error = null,
            ExecutionId = null
        };

        context.UserCodeRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new UserCodeRunDto
        {
            RunId = run.Id,
            Solution = run.Code,
            Language = run.Language,
            Input = run.Input,
            Status = run.Status,
            Verdict = run.Verdict,
            Output = run.Output,
            Error = run.Error,
            CreatedAt = run.CreatedAt
        };

        return dto;
    }
}
