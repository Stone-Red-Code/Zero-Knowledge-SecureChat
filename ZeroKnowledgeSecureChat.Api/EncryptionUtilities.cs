using System.Security.Cryptography;

namespace ZeroKnowledgeSecureChat.Api;

public static class EncryptionUtilities
{
    private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

    public static byte[] GenerateRandomKey(int length)
    {
        byte[] key = new byte[length];
        rng.GetBytes(key);
        return key;
    }

    // XOR-based OTP encryption/decryption
    public static byte[] XorBytes(byte[] data, byte[] key)
    {
        if (data.Length > key.Length)
        {
            throw new ArgumentException("Key must be at least as long as data");
        }

        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i]);
        }
        return result;
    }

    public static byte[] GenerateHmacSha256(byte[] data, byte[] key)
    {
        using HMACSHA256 hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    public static bool VerifyHmacSha256(byte[] data, byte[] key, byte[] hmac)
    {
        byte[] computedHmac = GenerateHmacSha256(data, key);
        return hmac.SequenceEqual(computedHmac);
    }

    public static byte[] GenerateHmacSha512(byte[] data, byte[] key)
    {
        using HMACSHA512 hmac = new HMACSHA512(key);
        return hmac.ComputeHash(data);
    }

    public static bool VerifyHmacSha512(byte[] data, byte[] key, byte[] hmac)
    {
        byte[] computedHmac = GenerateHmacSha512(data, key);
        return hmac.SequenceEqual(computedHmac);
    }
}