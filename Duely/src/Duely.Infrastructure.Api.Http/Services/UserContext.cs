using System.Security.Claims;
using Duely.Domain.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Services;

public interface IUserContext
{
    int UserId { get; }
}

public sealed class UserContext : IUserContext
{
    private readonly JwtTokenOptions _jwtTokenOptions;
    private readonly IEnumerable<Claim> _userClaims;

    public UserContext(IHttpContextAccessor httpContextAccessor, IOptions<JwtTokenOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor.HttpContext, nameof(httpContextAccessor.HttpContext));

        _userClaims = httpContextAccessor.HttpContext.User.Claims;
        _jwtTokenOptions = options.Value;
    }

    public int UserId
    {
        get
        {
            var value = _userClaims.Single(claim => claim.Type == _jwtTokenOptions.IdClaim).Value;
            return int.Parse(value);
        }
    } 
}
