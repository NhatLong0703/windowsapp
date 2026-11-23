using System.Security.Cryptography;

namespace WebApplication1.Helpers;

public static class Pw
{
    private const int Iter = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iter, HashAlgorithmName.SHA256, 32);
        return $"{Iter}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('.');
        if (p.Length != 3 || !int.TryParse(p[0], out var iter)) return false;
        var salt = Convert.FromBase64String(p[1]);
        var expected = Convert.FromBase64String(p[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
