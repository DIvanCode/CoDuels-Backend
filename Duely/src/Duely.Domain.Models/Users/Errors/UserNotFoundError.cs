using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Users.Errors;

public sealed class UserNotFoundError() : NotFoundError("Пользователь не найден.");
