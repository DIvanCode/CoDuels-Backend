using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Groups.Errors;

public sealed class GroupNotFoundError() : EntityNotFoundError("Группа не найдена.");
