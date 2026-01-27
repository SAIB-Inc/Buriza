namespace Buriza.Core.Crypto;

public static class KeyDerivationOptions
{
    public const string Algorithm = "PBKDF2";
    public const string Hash = "SHA-256";
    public const int Iterations = 600_000;

    public const string Encryption = "AES-GCM";
    public const int KeyLength = 256;

    public const int SaltSize = 32;
    public const int IvSize = 12;
    public const int TagSize = 16;
}

public static class CardanoDerivation
{
    public const int Purpose = 1852;
    public const int CoinType = 1815;

    public static string GetPath(int account, int role, int index)
        => $"m/{Purpose}'/{CoinType}'/{account}'/{role}/{index}";
}
