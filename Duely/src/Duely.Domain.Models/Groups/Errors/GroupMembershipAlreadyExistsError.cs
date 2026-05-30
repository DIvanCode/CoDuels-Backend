using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Groups.Errors;

public sealed class GroupMembershipAlreadyExistsError() : EntityAlreadyExistsError("Пользователь уже состоит в группе.");
