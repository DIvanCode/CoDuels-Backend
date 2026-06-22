using System.Text.RegularExpressions;

namespace Duely.Domain.Models.Users;

public static class UserConstants
{
    public static class Nickname
    {
        public const int MaxLength = 128;
        public static readonly Regex Regex = new("^[a-zA-Z0-9_-]*$", RegexOptions.Compiled);
    }

    public static class Password
    {
        public const int MinLength = 8;
        public const int MaxLength = 128;    
    }

    public static class IdentityTicket
    {
        public const int MaxLength = 1024;
    }
    
    public static class RefreshToken
    {
        public const int MaxLength = 1024;
    }
}
