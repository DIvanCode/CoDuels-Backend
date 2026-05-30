using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Groups.Errors;

public sealed class SetGroupRoleError() : ForbiddenError("У вас нет прав на назначение роли этому пользователю.");
