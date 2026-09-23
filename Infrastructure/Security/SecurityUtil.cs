using System.Security.Cryptography;

namespace evalflow_backend_api.Infrastructure.Security;

public static class SecurityUtil
{
    public static string Generate2FACode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
        var result = new char[6];

        for (var i = 0; i < 6; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }
}