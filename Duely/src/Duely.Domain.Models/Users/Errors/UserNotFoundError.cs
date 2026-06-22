using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Users.Errors;

public sealed class UserNotFoundError() : EntityNotFoundError("Пользователь не найден.");
