using System.Text;

namespace ZeroKnowledgeSecureChat.Api;

public class Message(string content)
{
    public string? Author { get; set; }
    public string Content { get; set; } = content;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Format: [Content length (2 bytes)][Content][New key]

    public static Message Decrypt(byte[] key, byte[] data, out byte[] newKey)
    {
        byte[] decryptedData = EncryptionUtilities.XorBytes(data, key);

        byte[] contentLengthBytes = decryptedData[..2];
        ushort contentLength = BitConverter.ToUInt16(contentLengthBytes);
        byte[] contentBytes = decryptedData[2..(2 + contentLength)];
        byte[] newKeyBytes = decryptedData[(2 + contentLength)..];

        string content = Encoding.UTF8.GetString(contentBytes);
        newKey = newKeyBytes;

        return new Message(content);
    }

    // Format: [Content length (2 bytes)][Content][New key]

    public byte[] Encrypt(byte[] key, out byte[] newKey)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(Content);
        byte[] contentLengthBytes = BitConverter.GetBytes((ushort)contentBytes.Length);

        if (key.Length - contentLengthBytes.Length - contentBytes.Length <= 0)
        {
            throw new ArgumentException("Key must be at least as long as data");
        }

        if (contentBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException("Content length must be less than or equal to 65535");
        }

        newKey = EncryptionUtilities.GenerateRandomKey(key.Length - contentLengthBytes.Length - contentBytes.Length);

        byte[] data = [.. contentLengthBytes, .. contentBytes, .. newKey];

        return EncryptionUtilities.XorBytes(data, key);
    }
}