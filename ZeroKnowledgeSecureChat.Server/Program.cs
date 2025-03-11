using CuteUtils.Misc;

using System.Collections.Concurrent;
using System.Net.WebSockets;

using WatsonWebsocket;

Uri uri = new Uri("http://localhost:9000");

try
{
    if (args.Length > 0)
    {
        uri = new Uri(args[0]);
    }
}
catch (UriFormatException)
{
    ConsoleExt.WriteLine("Invalid URI", ConsoleColor.Red);
    return;
}

ConcurrentDictionary<string, (Guid Client1, Guid Client2)> conversations = new();
ConcurrentDictionary<Guid, HashSet<string>> clientIds = new();

WatsonWsServer webSocketServer = new WatsonWsServer(uri);

webSocketServer.ClientConnected += WebSocketServer_ClientConnected;
webSocketServer.ClientDisconnected += WebSocketServer_ClientDisconnected;
webSocketServer.MessageReceived += WebSocketServer_MessageReceived;

Console.WriteLine("Starting WebSocket server...");

await webSocketServer.StartAsync();

Console.WriteLine("WebSocket server listening on " + uri);

await Task.Delay(-1);

void WebSocketServer_ClientConnected(object? sender, ConnectionEventArgs e)
{
    Console.WriteLine("Client connected: " + e.Client.Guid);
}

void WebSocketServer_ClientDisconnected(object? sender, DisconnectionEventArgs e)
{
    Console.WriteLine("Client disconnected: " + e.Client.Guid);

    _ = clientIds.TryRemove(e.Client.Guid, out HashSet<string>? conversationIds);

    if (conversationIds is null)
    {
        return;
    }

    lock (conversationIds)
    {
        foreach (string conversationId in conversationIds)
        {
            (Guid Client1, Guid Client2) conversation = conversations[conversationId];

            if (conversation.Client1 == e.Client.Guid)
            {
                _ = conversations.TryUpdate(conversationId, (Guid.Empty, conversation.Client2), conversation);
            }
            else if (conversation.Client2 == e.Client.Guid)
            {
                _ = conversations.TryUpdate(conversationId, (conversation.Client1, Guid.Empty), conversation);
            }

            if (conversations[conversationId].Client1 == Guid.Empty && conversations[conversationId].Client2 == Guid.Empty)
            {
                _ = conversations.TryRemove(conversationId, out _);
            }
        }
    }
}

void WebSocketServer_MessageReceived(object? sender, MessageReceivedEventArgs e)
{
    if (e.Data.Count < 64 || e.MessageType != WebSocketMessageType.Binary)
    {
        webSocketServer.DisconnectClient(e.Client.Guid);
        return;
    }

    string conversationId = Convert.ToBase64String(e.Data[..64]);

    _ = clientIds.TryAdd(e.Client.Guid, []);
    _ = conversations.TryAdd(conversationId, (Guid.Empty, Guid.Empty));

    if (!clientIds.TryGetValue(e.Client.Guid, out HashSet<string>? conversationIds))
    {
        return;
    }

    lock (conversationIds)
    {
        _ = conversationIds.Add(conversationId);
    }

    (Guid Client1, Guid Client2) conversation = conversations.GetValueOrDefault(conversationId);

    bool clientAlreadyInConversation = conversation.Client1 == e.Client.Guid || conversation.Client2 == e.Client.Guid;

    if (conversation.Client1 == Guid.Empty && !clientAlreadyInConversation)
    {
        _ = conversations.TryUpdate(conversationId, (e.Client.Guid, conversation.Client2), conversation);
        conversation = conversations.GetValueOrDefault(conversationId);
        Console.WriteLine($"Client {e.Client.Guid} connected to conversation {conversationId} as Client 1");
    }
    else if (conversation.Client2 == Guid.Empty && !clientAlreadyInConversation)
    {
        _ = conversations.TryUpdate(conversationId, (conversation.Client1, e.Client.Guid), conversation);
        conversation = conversations.GetValueOrDefault(conversationId);
        Console.WriteLine($"Client {e.Client.Guid} connected to conversation {conversationId} as Client 2");
    }

    if (conversation.Client1 == Guid.Empty || conversation.Client2 == Guid.Empty)
    {
        Console.WriteLine($"Client {e.Client.Guid} is waiting for the other client to connect to conversation {conversationId}");
        return;
    }

    if (conversation.Client1 == e.Client.Guid)
    {
        Console.WriteLine($"Sending message from Client 1 to Client 2 in conversation {conversationId}");
        _ = webSocketServer.SendAsync(conversation.Client2, e.Data);
    }
    else if (conversation.Client2 == e.Client.Guid)
    {
        Console.WriteLine($"Sending message from Client 2 to Client 1 in conversation {conversationId}");
        _ = webSocketServer.SendAsync(conversation.Client1, e.Data);
    }
    else
    {
        Console.WriteLine($"Client {e.Client.Guid} is not part of the conversation {conversationId}");
        webSocketServer.DisconnectClient(e.Client.Guid);
    }
}