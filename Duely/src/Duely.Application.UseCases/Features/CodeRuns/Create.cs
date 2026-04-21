using Duely.Application.Services.Errors;
using Duely.Application.Services.RateLimiting;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.CodeRuns;

public sealed class CreateCodeRunCommand : IRequest<Result<CodeRunDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
    public required char TaskKey { get; init; }
    public required int TimeLimit { get; init; }
    public required int MemoryLimit { get; init; }
    public required string Code { get; init; }
    public required Language Language { get; init; }
    public required string Input { get; init; }
}

public sealed class CreateCodeRunHandler(Context context, IRunUserCodeLimiter runUserCodeLimiter)
    : IRequestHandler<CreateCodeRunCommand, Result<CodeRunDto>>
{
    public async Task<Result<CodeRunDto>> Handle(CreateCodeRunCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        
        if (await runUserCodeLimiter.IsLimitExceededAsync(command.UserId, cancellationToken))
        {
            return new RateLimitExceededError("Too many code runs.");
        }
        
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        
        var codeRun = new CodeRun
        {
            User = user,
            DuelId = command.DuelId,
            TaskKey = command.TaskKey,
            Code = command.Code,
            Language = command.Language,
            Input = command.Input,
            Status = UserCodeRunStatus.Queued,
            HandledStatusCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        context.CodeRuns.Add(codeRun);
        await context.SaveChangesAsync(cancellationToken);

        var outboxMessage = new OutboxMessage
        {
            Type = OutboxType.RunUserCode,
            Payload = new RunCodePayload
            {
                RunId = codeRun.Id,
                Code = codeRun.Code,
                Language = codeRun.Language,
                Input = codeRun.Input,
                TimeLimit = command.TimeLimit,
                MemoryLimit = command.MemoryLimit
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };
        
        context.OutboxMessages.Add(outboxMessage);
        await context.SaveChangesAsync(cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);
        
        return new CodeRunDto
        {
            Id = codeRun.Id,
            Code = codeRun.Code,
            Language = codeRun.Language,
            Input = codeRun.Input,
            Status = codeRun.Status,
            Output = codeRun.Output,
            Error = codeRun.Error
        };
    }
}

public class CreateCodeRunCommandValidator : AbstractValidator<CreateCodeRunCommand>
{
    public CreateCodeRunCommandValidator()
    {
        RuleFor(x => x.DuelId).GreaterThan(0).WithMessage("DuelId must be positive.");
        RuleFor(x => x.TaskKey).Must(char.IsLetterOrDigit).WithMessage("Task key is invalid.");
        RuleFor(x => x.TimeLimit).GreaterThan(0).WithMessage("Time limit must be positive.");
        RuleFor(x => x.MemoryLimit).GreaterThan(0).WithMessage("Memory limit must be positive.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Language).IsInEnum().WithMessage("Language not recognized.");
        RuleFor(x => x.Input).MaximumLength(10000).WithMessage("Input is too long.");
    }
}
