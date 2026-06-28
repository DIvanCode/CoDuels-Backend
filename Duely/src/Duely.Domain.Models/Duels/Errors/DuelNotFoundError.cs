using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Duels.Errors;

public sealed class DuelNotFoundError() : NotFoundError("Дуэль не найдена.");
