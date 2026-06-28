using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Duels.Errors;

public sealed class DuelConfigurationNotFoundError() : NotFoundError("Настройки дуэли не найдены.");
