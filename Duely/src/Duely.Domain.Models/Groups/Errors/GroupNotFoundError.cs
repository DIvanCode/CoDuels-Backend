using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Groups.Errors;

public sealed class GroupNotFoundError() : EntityNotFoundError("Группа не найдена.");
