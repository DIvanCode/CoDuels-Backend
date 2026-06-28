using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.Handlers.Duels.UseCases.RankedDuels;

public sealed class CreateRankedDuelCommand : IRequest<Result>
{
    public required IReadOnlyCollection<int> Participants { get; init; }
}

internal sealed class CreateRankedDuelHandler(
    Context context,
    IOptions<DuelOptions> options,
    ILogger<CreateRankedDuelHandler> logger)
    : IRequestHandler<CreateRankedDuelCommand, Result>
{
    public async Task<Result> Handle(CreateRankedDuelCommand command, CancellationToken cancellationToken)
    {
        var participants = await context.Users
            .Where(u => command.Participants.Contains(u.Id))
            .ToListAsync(cancellationToken);
        if (participants.Count != command.Participants.Count)
        {
            return new NotFoundError("Один или несколько участников дуэли не найдены.");
        }
        
        var duelConfiguration = CreateDuelConfiguration();
        var rankedDuel = RankedDuel.Create(duelConfiguration);

        foreach (var participant in participants)
        {
            rankedDuel.AddParticipant(participant);
        }
        
        context.Duels.Add(rankedDuel);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Created ranked duel with users {Participants}",
            string.Join(", ", participants.Select(p => p.Nickname)));

        return Result.Ok();
    }

    private DuelConfiguration CreateDuelConfiguration()
    {
        var configuration = new DuelConfiguration(
            isRated: true,
            options.Value.DefaultConfiguration.ShouldShowOpponentSolution,
            options.Value.DefaultConfiguration.DurationMinutes,
            options.Value.DefaultConfiguration.ProblemsCount,
            options.Value.DefaultConfiguration.ProblemsOrder);

        context.DuelConfigurations.Add(configuration);
        
        return configuration;
    }
}

internal sealed class CreateRankedDuelCommandValidator : AbstractValidator<CreateRankedDuelCommand>
{
    public CreateRankedDuelCommandValidator()
    {
        RuleFor(x => x.Participants)
            .Must(x => x.Distinct().Count() == 2)
            .WithMessage("Рейтинговая дуэль проводится между двумя пользователями.");
    }
}
