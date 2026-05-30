using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Http;

internal interface IUserContext
{
    Guid UserId { get; }
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

    public Guid UserId
    {
        get
        {
            var value = _userClaims.Single(claim => claim.Type == IdClaim).Value;
            return Guid.Parse(value);
        }
    } 
}
