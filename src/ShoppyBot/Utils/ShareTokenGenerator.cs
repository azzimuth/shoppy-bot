using System.Security.Cryptography;

namespace ShoppyBot.Utils;

public static class ShareTokenGenerator
{
    private const int TokenLength = 32;

    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "")
            .Substring(0, TokenLength);
    }

    public static bool IsValidToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        return token.Length == TokenLength &&
               token.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}
