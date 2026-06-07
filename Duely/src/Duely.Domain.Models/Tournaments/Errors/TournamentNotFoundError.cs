using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Tournaments.Errors;

public sealed class TournamentNotFoundError() : EntityNotFoundError("Турнир не найден.");
