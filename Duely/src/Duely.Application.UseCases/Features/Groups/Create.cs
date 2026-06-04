using Duely.Application.UseCases.Dto.Groups;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class CreateGroupCommand : IRequest<Result<GroupShortDto>>
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
}

internal sealed class CreateGroupHandler(Context context, ILogger<CreateGroupHandler> logger)
    : IRequestHandler<CreateGroupCommand, Result<GroupShortDto>>
{
    public async Task<Result<GroupShortDto>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }

        var groupId = new GroupId(Guid.NewGuid());
        var groupName = new GroupName(command.Name);
        var group = new Group(groupId, groupName);
        
        var groupMembershipId = new GroupMembershipId(Guid.NewGuid());
        var groupMembership = new GroupMembership(groupMembershipId, user, group, GroupRole.Manager, isConfirmed: true);
        
        context.Groups.Add(group);
        context.GroupMemberships.Add(groupMembership);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} created group {GroupId}", user.Nickname, group.Id);

        return new GroupShortDto
        {
            Id = group.Id,
            Name = group.Name.Value,
            Membership = new GroupMembershipShortDto
            {
                Role = groupMembership.Role,
                IsConfirmed = groupMembership.IsConfirmed
            }
        };
    }
}

internal sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название группы не может быть пустым.")
            .MaximumLength(GroupName.MaxLength).WithMessage($"Название группы не может содержать более {GroupName.MaxLength} символов.");
    }
}
