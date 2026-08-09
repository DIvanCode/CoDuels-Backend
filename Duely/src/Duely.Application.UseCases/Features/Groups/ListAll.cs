using Duely.Application.UseCases.Dtos;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetAllGroupsQuery : IRequest<Result<List<GroupListItemDto>>>;

public sealed class GetAllGroupsHandler(Context context)
    : IRequestHandler<GetAllGroupsQuery, Result<List<GroupListItemDto>>>
{
    public async Task<Result<List<GroupListItemDto>>> Handle(
        GetAllGroupsQuery query,
        CancellationToken cancellationToken)
    {
        return await context.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .Select(group => new GroupListItemDto
            {
                Id = group.Id,
                Name = group.Name
            })
            .ToListAsync(cancellationToken);
    }
}
