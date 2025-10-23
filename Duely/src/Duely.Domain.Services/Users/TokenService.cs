using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Duely.Domain.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Duely.Domain.Services.Users;

public interface ITokenService
{
    (string AccessToken, string RefreshToken) GenerateTokens(User user);
}

public sealed class TokenService(IOptions<JwtTokenOptions> jwtTokenOptions) : ITokenService
{
    public (string AccessToken, string RefreshToken) GenerateTokens(User user)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtTokenOptions.Value.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new Claim[]
        {
            new(jwtTokenOptions.Value.IdClaim, user.Id.ToString()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            signingCredentials: signingCredentials,
            expires: DateTime.UtcNow.AddHours(jwtTokenOptions.Value.ExpiresHours));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Guid.NewGuid();

        return (accessToken, refreshToken.ToString());
    }
}
