using System.Text;

namespace ZeroKnowledgeSecureChat.Api;

public class Message(string content)
{
    public string? Author { get; set; }
    public string Content { get; set; } = content;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Format: [HMAC (32 bytes)][Content length (2 bytes)][Content][New key]

    public static Message Decrypt(byte[] key, byte[] signingKey, byte[] data, out byte[] newKey)
    {
        byte[] decryptedData = EncryptionUtilities.XorBytes(data, key);
        byte[] hmac = decryptedData[..32];

        if (!EncryptionUtilities.VerifyHmacSha256(decryptedData[32..], signingKey, hmac))
        {
            throw new ArgumentException("HMAC does not match");
        }

        byte[] contentLengthBytes = decryptedData[32..34];
        ushort contentLength = BitConverter.ToUInt16(contentLengthBytes);
        byte[] contentBytes = decryptedData[34..(34 + contentLength)];
        byte[] newKeyBytes = decryptedData[(34 + contentLength)..];

        string content = Encoding.UTF8.GetString(contentBytes);
        newKey = newKeyBytes;

        return new Message(content);
    }

    // Format: [HMAC (32 bytes)][Content length (2 bytes)][Content][New key]

    public byte[] Encrypt(byte[] key, byte[] signingKey, out byte[] newKey)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(Content);
        byte[] contentLengthBytes = BitConverter.GetBytes((ushort)contentBytes.Length);

        if (key.Length - 32 - contentLengthBytes.Length - contentBytes.Length <= 0)
        {
            throw new ArgumentException("Key must be at least as long as data");
        }

        if (contentBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException("Content length must be less than or equal to 65535");
        }

        newKey = EncryptionUtilities.GenerateRandomKey(key.Length - 32 - contentLengthBytes.Length - contentBytes.Length);

        byte[] data = [.. contentLengthBytes, .. contentBytes, .. newKey];
        byte[] hmac = EncryptionUtilities.GenerateHmacSha256(data, signingKey);
        byte[] signedData = [.. hmac, .. data];

        return EncryptionUtilities.XorBytes(signedData, key);
    }
}