using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Http;

public interface IUserContext
{
    int UserId { get; }
}

internal sealed class UserContext : IUserContext
{
    public const string IdClaim = "id";
    
    private readonly IEnumerable<Claim> _userClaims;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor.HttpContext, nameof(httpContextAccessor.HttpContext));

        _userClaims = httpContextAccessor.HttpContext.User.Claims;
    }

    public int UserId => int.Parse(_userClaims.Single(claim => claim.Type == IdClaim).Value);
}
