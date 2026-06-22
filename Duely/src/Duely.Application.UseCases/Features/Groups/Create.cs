// using Duely.Application.UseCases.Dto.Groups;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Entities;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.UseCases.Features.Groups;
//
// public sealed class CreateGroupCommand : IRequest<Result<GroupShortDto>>
// {
//     public required Guid UserId { get; init; }
//     public required string Name { get; init; }
// }
//
// internal sealed class CreateGroupHandler(Context context, ILogger<CreateGroupHandler> logger)
//     : IRequestHandler<CreateGroupCommand, Result<GroupShortDto>>
// {
//     public async Task<Result<GroupShortDto>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Include(u => u.Nickname)
//             .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//
//         var id = Guid.NewGuid();
//         var group = new Group(id, command.Name);
//         var membership = group.CreateMembership(user, GroupRole.Manager, isConfirmed: true);
//         
//         context.Groups.Add(group);
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation("User {Nickname} created group {GroupId}", user.Nickname, group.Id);
//
//         return new GroupShortDto
//         {
//             Id = group.Id,
//             Name = group.Name,
//             Membership = new GroupMembershipShortDto
//             {
//                 Role = membership.Role,
//                 IsConfirmed = membership.IsConfirmed
//             }
//         };
//     }
// }
//
// internal sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
// {
//     public CreateGroupCommandValidator()
//     {
//         RuleFor(x => x.Name)
//             .NotEmpty()
//             .WithMessage("Название группы не может быть пустым.");
//         RuleFor(x => x.Name)
//             .MaximumLength(GroupConstants.Name.MaxLength)
//             .WithMessage($"Название группы не может содержать более {GroupConstants.Name.MaxLength} символов.");
//     }
// }
