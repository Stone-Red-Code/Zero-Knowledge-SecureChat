namespace ZeroKnowledgeSecureChat.Api.Tests;

internal class TestChatClient(string name, byte[] key, byte[] signingKey) : ChatClient(name, key, signingKey)
{
    public event EventHandler<byte[]>? SendData;

    public override async Task TransmitData(byte[] data)
    {
        // Simulate network delay
        await Task.Delay(1000);

        SendData?.Invoke(this, data);
    }

    public async Task ReceiveData(byte[] data)
    {
        await ProcessData(data);
    }
}