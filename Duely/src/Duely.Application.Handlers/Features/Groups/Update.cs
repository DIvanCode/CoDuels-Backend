// using Duely.Application.Handlers.Dto.Groups;
// using Duely.Domain.Kernel.Errors;
// using Duely.Domain.Models.Groups.Entities;
// using Duely.Domain.Models.Groups.Errors;
// using Duely.Domain.Services.Groups;
// using Duely.Infrastructure.DataAccess.EntityFramework;
// using FluentResults;
// using FluentValidation;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Duely.Application.Handlers.Features.Groups;
//
// public sealed class UpdateGroupCommand : IRequest<Result<GroupShortDto>>
// {
//     public required Guid UserId { get; init; }
//     public required Guid GroupId { get; init; }
//     public required string Name { get; init; }
// }
//
// internal sealed class UpdateGroupHandler(
//     Context context,
//     IGroupPermissionsService groupPermissionsService,
//     ILogger<UpdateGroupHandler> logger)
//     : IRequestHandler<UpdateGroupCommand, Result<GroupShortDto>>
// {
//     public async Task<Result<GroupShortDto>> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
//     {
//         var user = await context.Users
//             .AsNoTracking()
//             .Where(u => u.Id == command.UserId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (user is null)
//         {
//             return new ForbiddenError();
//         }
//         
//         var group = await context.Groups
//             .Include(g => g.Memberships.Where(m => m.User.Id == command.UserId))
//             .ThenInclude(m => m.User)
//             .Where(g => g.Id == command.GroupId)
//             .SingleOrDefaultAsync(cancellationToken);
//         if (group is null)
//         {
//             return new GroupNotFoundError();
//         }
//
//         var membership = group.GetMembership(user);
//         if (membership is null || !groupPermissionsService.CanUpdateGroup(membership))
//         {
//             return new ForbiddenError("У вас нет прав для редактирования этой группы.");
//         }
//
//         group.UpdateName(command.Name);
//         
//         await context.SaveChangesAsync(cancellationToken);
//         
//         logger.LogInformation("User {Nickname} updated group {GroupId}", user.Nickname, group.Id);
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
// internal sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
// {
//     public UpdateGroupCommandValidator()
//     {
//         RuleFor(x => x.Name)
//             .NotEmpty()
//             .WithMessage("Название группы не может быть пустым.");
//         RuleFor(x => x.Name)
//             .MaximumLength(GroupConstants.Name.MaxLength)
//             .WithMessage($"Название группы не может содержать более {GroupConstants.Name.MaxLength} символов.");
//     }
// }
