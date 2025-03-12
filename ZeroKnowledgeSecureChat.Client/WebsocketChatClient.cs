using WatsonWebsocket;

using ZeroKnowledgeSecureChat.Api;

namespace ZeroKnowledgeSecureChat;

public class WebSocketChatClient : ChatClient
{
    private readonly WatsonWsClient webSocketClient;

    public bool IsConnected => webSocketClient?.Connected ?? false;

    public WebSocketChatClient(ChatClientState chatClientState, WatsonWsClient webSocketClient) : base(chatClientState)
    {
        this.webSocketClient = webSocketClient;
        webSocketClient.MessageReceived += WebSocketClient_MessageReceived;
        _ = webSocketClient.SendAsync(ConversationId);
    }

    public WebSocketChatClient(string name, int initialKeyLength, WatsonWsClient webSocketClient) : base(name, initialKeyLength)
    {
        this.webSocketClient = webSocketClient;
        webSocketClient.MessageReceived += WebSocketClient_MessageReceived;
        _ = webSocketClient.SendAsync(ConversationId);
    }

    public WebSocketChatClient(string name, byte[] key, byte[] signingKey, WatsonWsClient webSocketClient) : base(name, key, signingKey)
    {
        this.webSocketClient = webSocketClient;
        webSocketClient.MessageReceived += WebSocketClient_MessageReceived;
        _ = webSocketClient.SendAsync(ConversationId);
    }

    public override async Task<bool> TransmitData(byte[] data)
    {
        if (webSocketClient == null || !webSocketClient.Connected)
        {
            return false;
        }

        return await webSocketClient.SendAsync([.. ConversationId, .. data]);
    }

    private async void WebSocketClient_MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (!e.Data[..64].SequenceEqual(ConversationId))
        {
            return;
        }

        await ProcessData([.. e.Data[64..]]);
    }
}