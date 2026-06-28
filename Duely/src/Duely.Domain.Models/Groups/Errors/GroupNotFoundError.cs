using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Groups.Errors;

public sealed class GroupNotFoundError() : NotFoundError("Группа не найдена.");
