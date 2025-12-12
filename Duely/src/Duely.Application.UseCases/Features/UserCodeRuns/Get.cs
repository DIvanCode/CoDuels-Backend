using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.UserCodeRuns;

public sealed class GetUserCodeRunQuery : IRequest<Result<UserCodeRunDto>>
{
    public required int UserId { get; init; }
    public required int RunId { get; init; }
}

public sealed class GetUserCodeRunHandler(Context context)
    : IRequestHandler<GetUserCodeRunQuery, Result<UserCodeRunDto>>
{
    public async Task<Result<UserCodeRunDto>> Handle(GetUserCodeRunQuery query, CancellationToken cancellationToken)
    {
        var run = await context.UserCodeRuns
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Id == query.RunId, cancellationToken);

        if (run is null)
        {
            return new EntityNotFoundError(nameof(UserCodeRun), nameof(UserCodeRun.Id), query.RunId);
        }

        if (run.User.Id != query.UserId)
        {
            return new ForbiddenError(nameof(UserCodeRun), "get", nameof(UserCodeRun.Id), query.RunId);
        }

        return new UserCodeRunDto
        {
            RunId = run.Id,
            Code = run.Code,
            Language = run.Language,
            Input = run.Input,
            Status = run.Status,
            Output = run.Output,
            Error = run.Error
        };
    }
}
