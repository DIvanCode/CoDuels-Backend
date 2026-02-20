using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class UpdateGroupCommand : IRequest<Result<GroupDto>>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateGroupHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<UpdateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Where(g => g.Id == request.GroupId)
            .Include(g => g.Users.Where(u => u.User.Id == request.UserId))
            .SingleOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), request.GroupId);
        }

        var userGroupRole = group.Users.SingleOrDefault();
        if (userGroupRole is null || !groupPermissionsService.HasUpdatePermission(userGroupRole.Role))
        {
            return new ForbiddenError(nameof(Group), "update", nameof(Group.Id), request.GroupId);
        }

        group.Name = request.Name;
        await context.SaveChangesAsync(cancellationToken);

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name
        };
    }
}

public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("group name is required");
    }
}
