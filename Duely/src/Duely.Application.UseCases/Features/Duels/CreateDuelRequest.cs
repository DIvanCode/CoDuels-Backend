using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CreateDuelRequestCommand : IRequest<Result<DuelRequestDto>>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public required int ConfigurationId { get; init; }
}

public sealed class CreateDuelRequestHandler(Context context)
    : IRequestHandler<CreateDuelRequestCommand, Result<DuelRequestDto>>
{
    public async Task<Result<DuelRequestDto>> Handle(CreateDuelRequestCommand command, CancellationToken cancellationToken)
    {
        var requester = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (requester is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var opponent = await context.Users.SingleOrDefaultAsync(
            u => u.Nickname == command.OpponentNickname,
            cancellationToken);
        if (opponent is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        if (opponent.Id == requester.Id)
        {
            return new ForbiddenError(nameof(Duel), "create", nameof(User.Id), requester.Id);
        }

        var configuration = await context.DuelConfigurations.SingleOrDefaultAsync(
            c => c.Id == command.ConfigurationId,
            cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), command.ConfigurationId);
        }

        var existingRequest = await context.Duels.AnyAsync(
            d => d.Status == DuelStatus.Pending &&
                 ((d.User1.Id == requester.Id && d.User2.Id == opponent.Id) ||
                  (d.User1.Id == opponent.Id && d.User2.Id == requester.Id)),
            cancellationToken);
        if (existingRequest)
        {
            return new EntityAlreadyExistsError(nameof(Duel), "pending request");
        }

        var startTime = DateTime.UtcNow;
        var duel = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = startTime,
            DeadlineTime = startTime.AddMinutes(configuration.MaxDurationMinutes),
            User1 = requester,
            User1InitRating = requester.Rating,
            User2 = opponent,
            User2InitRating = opponent.Rating
        };

        context.Duels.Add(duel);
        await context.SaveChangesAsync(cancellationToken);

        return new DuelRequestDto
        {
            Id = duel.Id,
            ConfigurationId = configuration.Id,
            OpponentNickname = opponent.Nickname,
            CreatedAt = duel.StartTime
        };
    }
}
