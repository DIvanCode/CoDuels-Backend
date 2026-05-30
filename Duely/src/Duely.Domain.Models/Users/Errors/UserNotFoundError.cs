using Duely.Domain.Common.Errors;

namespace Duely.Domain.Models.Users.Errors;

public sealed class UserNotFoundError() : EntityNotFoundError("Пользователь не найден.");
