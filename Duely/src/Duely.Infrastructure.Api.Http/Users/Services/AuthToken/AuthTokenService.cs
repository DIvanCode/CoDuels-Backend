using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Duely.Application.UseCases.Dto.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Duely.Infrastructure.Api.Http.Users.Services.AuthToken;

internal interface IAuthTokenService
{
    string GenerateAuthToken(UserDto userDto);
}

internal sealed class JwtAuthService(IOptions<JwtTokenOptions> jwtTokenOptions) : IAuthTokenService
{
    public string GenerateAuthToken(UserDto userDto)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtTokenOptions.Value.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new Claim[]
        {
            new(UserContext.IdClaim, userDto.Id.ToString()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            signingCredentials: signingCredentials,
            expires: DateTime.UtcNow.AddHours(jwtTokenOptions.Value.ExpiresHours));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
