using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Duels.Errors;

public sealed class DuelNotFoundError() : EntityNotFoundError("Дуэль не найдена.");
