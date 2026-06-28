using Duely.Domain.Kernel.Errors;

namespace Duely.Domain.Models.Users.Errors;

public sealed class RefreshTokenAlreadyExistsError()
    : InvalidOperationError("Пользователь с заданным обменным токеном уже существует.");
