using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.CodeRuns;

public sealed class GetCodeRunQuery : IRequest<Result<CodeRunDto>>
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
}

public sealed class GetCodeRunHandler(Context context)
    : IRequestHandler<GetCodeRunQuery, Result<CodeRunDto>>
{
    public async Task<Result<CodeRunDto>> Handle(GetCodeRunQuery query, CancellationToken cancellationToken)
    {
        var codeRun = await context.CodeRuns
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Id == query.Id, cancellationToken);
        if (codeRun is null)
        {
            return new EntityNotFoundError(nameof(CodeRun), nameof(CodeRun.Id), query.Id);
        }

        if (codeRun.User.Id != query.UserId)
        {
            return new ForbiddenError(nameof(CodeRun), "get", nameof(CodeRun.Id), query.Id);
        }

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
