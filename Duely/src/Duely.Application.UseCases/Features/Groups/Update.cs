using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Groups;
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
    private const string Operation = "update";
    
    public async Task<Result<GroupDto>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), request.GroupId);
        }

        var membership = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == group.Id && m.User.Id == request.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || !groupPermissionsService.CanUpdateGroup(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), request.GroupId);
        }

        group.Name = request.Name;
        await context.SaveChangesAsync(cancellationToken);

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            UserRole = membership.Role
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
