using System.Security.Cryptography;
using System.Text;

namespace SmartBodyAI.Helpers;

public static class PkceHelper
{
    public static string GenerateCodeVerifier(int size = 32)
    {
        byte[] bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        byte[] hashBytes = SHA256.HashData(verifierBytes);
        return Base64UrlEncode(hashBytes);
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
