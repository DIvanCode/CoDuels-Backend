using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Duels.Errors;

public sealed class DuelConfigurationNotFoundError() : EntityNotFoundError("Настройки дуэли не найдены.");
