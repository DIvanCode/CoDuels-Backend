using Duely.Application.UseCases.Dto.Groups;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class UpdateGroupCommand : IRequest<Result<GroupShortDto>>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateGroupHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ILogger<UpdateGroupHandler> logger)
    : IRequestHandler<UpdateGroupCommand, Result<GroupShortDto>>
{
    public async Task<Result<GroupShortDto>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var group = await context.Groups
            .Include(g => g.Name)
            .SingleOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }

        var membership = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == group.Id && m.User.Id == request.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || !groupPermissionsService.CanUpdateGroup(membership))
        {
            return new ForbiddenError("У вас нет прав для редактирования этой группы.");
        }

        var name = new GroupName(request.Name);
        group.UpdateName(name);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} updated group {GroupId}", user.Nickname, group.Id);

        return new GroupShortDto
        {
            Id = group.Id,
            Name = group.Name.Value,
            Membership = new GroupMembershipShortDto
            {
                Role = membership.Role,
                IsConfirmed = membership.IsConfirmed
            }
        };
    }
}

public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название группы не может быть пустым.")
            .MaximumLength(GroupName.MaxLength).WithMessage($"Название группы не может содержать более {GroupName.MaxLength} символов.");
    }
}
