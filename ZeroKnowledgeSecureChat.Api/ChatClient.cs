using FluentResults;

using System.Collections.Concurrent;
using System.Text;

namespace ZeroKnowledgeSecureChat.Api;

public class ChatClientState
{
    public required string Name { get; init; } = null!;
    public required byte[] CurrentKey { get; set; } = null!;
    public required byte[] SigningKey { get; init; } = null!;
    public required int InitialKeyLength { get; init; }
    public ConcurrentBag<Message> Messages { get; init; } = [];
}

public abstract class ChatClient
{
    public event EventHandler<Message>? MessageReceived;

    public event EventHandler<SendReceiveState>? SendReceiveStateChanged;

    public event EventHandler<string>? ErrorReceived;

    private readonly SemaphoreSlim semaphoreSlim = new(1, 1);

    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private SendReceiveState sendReceiveState = SendReceiveState.None;

    public SendReceiveState SendReceiveState
    {
        get => sendReceiveState;
        private set
        {
            sendReceiveState = value;
            SendReceiveStateChanged?.Invoke(this, value);
        }
    }

    public byte[] ConversationId { get; private init; }

    public string Name => ChatClientState.Name;

    public byte[] CurrentKey
    {
        get => ChatClientState.CurrentKey;
        private set => ChatClientState.CurrentKey = value;
    }

    public byte[] SigningKey => ChatClientState.SigningKey;
    public int CurrentKeyLength => ChatClientState.CurrentKey.Length;
    public int SigningKeyLength => ChatClientState.SigningKey.Length;
    public int InitialKeyLength => ChatClientState.InitialKeyLength;

    public ChatClientState ChatClientState { get; } = null!;

    public ConcurrentBag<Message> Messages => ChatClientState.Messages;

    protected ChatClient(string name, int initialKeyLength)
    {
        ChatClientState = new ChatClientState()
        {
            Name = name,
            CurrentKey = EncryptionUtilities.GenerateRandomKey(initialKeyLength),
            SigningKey = EncryptionUtilities.GenerateRandomKey(512),
            InitialKeyLength = initialKeyLength
        };

        ConversationId = EncryptionUtilities.GenerateHmacSha512([], ChatClientState.SigningKey);
    }

    protected ChatClient(string name, byte[] key, byte[] signingKey)
    {
        ChatClientState = new ChatClientState()
        {
            Name = name,
            CurrentKey = key,
            SigningKey = signingKey,
            InitialKeyLength = key.Length
        };

        ConversationId = EncryptionUtilities.GenerateHmacSha512([], ChatClientState.SigningKey);
    }

    protected ChatClient(ChatClientState chatClientState)
    {
        ChatClientState = chatClientState;
        ConversationId = EncryptionUtilities.GenerateHmacSha512([], chatClientState.SigningKey);
    }

    public async Task<Result> SendMessage(string content)
    {
        await semaphoreSlim.WaitAsync();

        if (SendReceiveState != SendReceiveState.None)
        {
            _ = semaphoreSlim.Release();
            return Result.Fail("Cannot send message while in a send/receive state");
        }

        SendReceiveState = SendReceiveState.WaitForPong;

        if (!await SendData(ProtocolMessageType.Ping))
        {
            return Result.Fail("Failed to send ping");
        }

        try
        {
            await TaskExtensions.WaitUntil(() => SendReceiveState == SendReceiveState.PongReceived, timeout);
        }
        catch (TimeoutException)
        {
            SendReceiveState = SendReceiveState.None;
            _ = semaphoreSlim.Release();
            return Result.Fail("Timed out waiting for pong");
        }

        SendReceiveState = SendReceiveState.WaitForSendPermission;

        Message message = new Message(content);
        byte[] encryptedData;
        byte[] newKey;

        try
        {
            encryptedData = message.Encrypt(CurrentKey, out newKey);
        }
        catch (ArgumentException ex)
        {
            SendReceiveState = SendReceiveState.None;
            _ = semaphoreSlim.Release();
            return Result.Fail(ex.Message);
        }

        if (!await SendData(ProtocolMessageType.MessageRequest))
        {
            return Result.Fail("Failed to send message request");
        }

        try
        {
            await TaskExtensions.WaitUntil(() => SendReceiveState == SendReceiveState.SendAllowed, timeout);
        }
        catch (TimeoutException)
        {
            SendReceiveState = SendReceiveState.None;
            _ = semaphoreSlim.Release();
            return Result.Fail("Timed out waiting for send permission");
        }

        SendReceiveState = SendReceiveState.WaitForMessageAccept;

        if (!await SendData(ProtocolMessageType.Message, encryptedData))
        {
            return Result.Fail("Failed to send message");
        }

        try
        {
            await TaskExtensions.WaitUntil(() => SendReceiveState == SendReceiveState.MessageAccepted, timeout);
        }
        catch (TimeoutException)
        {
            SendReceiveState = SendReceiveState.None;
            _ = semaphoreSlim.Release();
            return Result.Fail("Timed out waiting for message accept");
        }

        CurrentKey = newKey;

        SendReceiveState = SendReceiveState.None;

        Messages.Add(message);

        _ = semaphoreSlim.Release();
        return Result.Ok();
    }

    public async Task<Result> SendPing()
    {
        await semaphoreSlim.WaitAsync();

        if (SendReceiveState != SendReceiveState.None)
        {
            _ = semaphoreSlim.Release();
            return Result.Fail("Cannot send ping while in a send/receive state");
        }

        SendReceiveState = SendReceiveState.WaitForPong;

        _ = await SendData(ProtocolMessageType.Ping);

        try
        {
            await TaskExtensions.WaitUntil(() => SendReceiveState == SendReceiveState.PongReceived, timeout);
        }
        catch (TimeoutException)
        {
            SendReceiveState = SendReceiveState.None;
            _ = semaphoreSlim.Release();
            return Result.Fail("Timed out waiting for pong");
        }

        SendReceiveState = SendReceiveState.None;

        _ = semaphoreSlim.Release();

        return Result.Ok();
    }

    public abstract Task<bool> TransmitData(byte[] data);

    protected async Task ProcessData(byte[] data)
    {
        byte[] hmac = data[..64];

        if (!EncryptionUtilities.VerifyHmacSha512(data[64..], SigningKey, hmac))
        {
            _ = await SendData(ProtocolMessageType.Error, "HMAC does not match");
            return;
        }

        data = data[64..];

        if (data.Length <= 1)
        {
            _ = await SendData(ProtocolMessageType.Error, "Data length must be at least 2 bytes");
            return;
        }

        byte version = data[0];

        if (version != 1)
        {
            _ = await SendData(ProtocolMessageType.Error, "Unsupported version");
            return;
        }

        byte[] timeStamp = data[2..10];

        if (BitConverter.ToInt64(timeStamp) < (DateTimeOffset.UtcNow - timeout).ToUnixTimeSeconds())
        {
            _ = await SendData(ProtocolMessageType.Error, "Data is too old");
            return;
        }

        ProtocolMessageType type = (ProtocolMessageType)data[1];

        switch (type)
        {
            case ProtocolMessageType.Message:
                _ = await ReceiveMessage(data[10..]);
                break;

            case ProtocolMessageType.Ping:
                _ = await SendData(ProtocolMessageType.Pong);
                break;

            case ProtocolMessageType.Pong:
                SendReceiveState = SendReceiveState == SendReceiveState.WaitForPong ? SendReceiveState.PongReceived : SendReceiveState;
                break;

            case ProtocolMessageType.MessageRequest:
                _ = await ProcessMessageRequest();
                break;

            case ProtocolMessageType.MessageRequestCancel:
                SendReceiveState = SendReceiveState == SendReceiveState.WaitForSendPermission ? SendReceiveState.None : SendReceiveState;
                break;

            case ProtocolMessageType.MessageRequestAccept:
                SendReceiveState = SendReceiveState == SendReceiveState.WaitForSendPermission ? SendReceiveState.SendAllowed : SendReceiveState;
                break;

            case ProtocolMessageType.MessageRequestDeny:
                SendReceiveState = SendReceiveState == SendReceiveState.WaitForSendPermission ? SendReceiveState.None : SendReceiveState;
                break;

            case ProtocolMessageType.MessageReceived:
                SendReceiveState = SendReceiveState == SendReceiveState.WaitForMessageAccept ? SendReceiveState.MessageAccepted : SendReceiveState;
                break;

            case ProtocolMessageType.Error:
                ProcessError(data[10..]);
                break;

            default:
                _ = await SendData(ProtocolMessageType.Error);
                break;
        }
    }

    private async Task<Result> ReceiveMessage(byte[] encryptedData)
    {
        if (SendReceiveState != SendReceiveState.WaitForMessage)
        {
            _ = await SendData(ProtocolMessageType.Error);
        }

        Message message;
        byte[] newKey;

        try
        {
            message = Message.Decrypt(CurrentKey, encryptedData, out newKey);
            message.Author = Name;
        }
        catch (ArgumentException ex)
        {
            SendReceiveState = SendReceiveState.None;
            _ = await SendData(ProtocolMessageType.Error, ex.Message);
            return Result.Fail(ex.Message);
        }

        _ = await SendData(ProtocolMessageType.MessageReceived);

        CurrentKey = newKey;

        SendReceiveState = SendReceiveState.None;

        Messages.Add(message);

        MessageReceived?.Invoke(this, message);

        return Result.Ok();
    }

    private Task<bool> ProcessMessageRequest()
    {
        ProtocolMessageType messageType = SendReceiveState == SendReceiveState.None ? ProtocolMessageType.MessageRequestAccept : ProtocolMessageType.MessageRequestDeny;

        if (messageType == ProtocolMessageType.MessageRequestAccept)
        {
            SendReceiveState = SendReceiveState.WaitForMessage;
        }

        return SendData(messageType);
    }

    private void ProcessError(byte[] data)
    {
        try
        {
            string errorMessage = Encoding.UTF8.GetString(data);
            ErrorReceived?.Invoke(this, errorMessage);
        }
        catch (Exception)
        {
            ErrorReceived?.Invoke(this, "Unknown error");
        }

        SendReceiveState = SendReceiveState.None;
    }

    // Format: [HMAC (64 bytes)][Version (1 byte)][Type (1 byte)][Timestamp (8 bytes)][Data]
    private Task<bool> SendData(ProtocolMessageType type, byte[]? data = null)
    {
        data ??= [];

        byte[] timeStampBytes = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        byte[] extendedData = [1, (byte)type, .. timeStampBytes, .. data];

        byte[] hmac = EncryptionUtilities.GenerateHmacSha512(extendedData, SigningKey);
        byte[] signedData = [.. hmac, .. extendedData];

        return TransmitData(signedData);
    }

    private Task<bool> SendData(ProtocolMessageType type, string content)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        return SendData(type, contentBytes);
    }
}