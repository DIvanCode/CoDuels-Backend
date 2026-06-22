using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Duels.Errors;

public sealed class DuelConfigurationNotFoundError() : EntityNotFoundError("Настройки дуэли не найдены.");
