using System.Security.Cryptography;

namespace Duely.Infrastructure.Api.Http.Users.Services.IdentityTicket;

public interface IIdentityTicketService
{
    string GenerateIdentityTicket();
}

internal sealed class IdentityTicketService : IIdentityTicketService
{
    private const int IdentityTicketBytesLength = 32;
    
    public string GenerateIdentityTicket()
    {
        var bytes = new byte[IdentityTicketBytesLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
