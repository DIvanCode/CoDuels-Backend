using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class CreateGroupCommand : IRequest<Result<GroupDto>>
{
    public required int UserId { get; init; }
    public required string Name { get; init; }
}

public sealed class CreateGroupHandler(Context context)
    : IRequestHandler<CreateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), request.UserId);
        }

        var group = new Group
        {
            Name = request.Name
        };
        
        group.Users.Add(new GroupMembership
        {
            User = user,
            Group = group,
            Role = GroupRole.Creator
        });
        
        context.Groups.Add(group);
        await context.SaveChangesAsync(cancellationToken);

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            UserRole = GroupRole.Creator
        };
    }
}

public sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("group name is required");
    }
}
