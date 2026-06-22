using System.Security.Cryptography;

namespace Duely.Infrastructure.Api.Http.Users.Services.RefreshToken;

public interface IRefreshTokenService
{
    string GenerateRefreshToken();
}

internal sealed class RefreshTokenService : IRefreshTokenService
{
    private const int RefreshTokenBytesLength = 32;
    
    public string GenerateRefreshToken()
    {
        var bytes = new byte[RefreshTokenBytesLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
