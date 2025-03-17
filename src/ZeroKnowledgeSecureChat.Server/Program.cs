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

ConcurrentDictionary<string, Conversation> conversations = new();
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
            Conversation conversation = conversations[conversationId];

            if (conversation.Client1 == e.Client.Guid)
            {
                conversation.Client1 = Guid.Empty;
                conversations[conversationId] = conversation;
            }
            else if (conversation.Client2 == e.Client.Guid)
            {
                conversation.Client2 = Guid.Empty;
                conversations[conversationId] = conversation;
            }

            if (conversations[conversationId].Client1 == Guid.Empty && conversations[conversationId].Client2 == Guid.Empty)
            {
                _ = conversations.TryRemove(conversationId, out _);
            }
        }
    }
}

async void WebSocketServer_MessageReceived(object? sender, MessageReceivedEventArgs e)
{
    if (e.Data.Count < 64 || e.MessageType != WebSocketMessageType.Binary)
    {
        webSocketServer.DisconnectClient(e.Client.Guid);
        return;
    }

    string conversationId = Convert.ToBase64String(e.Data[..64]);

    _ = clientIds.TryAdd(e.Client.Guid, []);
    _ = conversations.TryAdd(conversationId, new Conversation(Guid.Empty, Guid.Empty, new(), new()));

    if (!clientIds.TryGetValue(e.Client.Guid, out HashSet<string>? conversationIds))
    {
        return;
    }

    lock (conversationIds)
    {
        _ = conversationIds.Add(conversationId);
    }

    Conversation conversation = conversations.GetValueOrDefault(conversationId);

    bool clientAlreadyInConversation = conversation.Client1 == e.Client.Guid || conversation.Client2 == e.Client.Guid;

    if (conversation.Client1 == Guid.Empty && !clientAlreadyInConversation)
    {
        conversation.Client1 = e.Client.Guid;
        conversations[conversationId] = conversation;
        Console.WriteLine($"Client {e.Client.Guid} connected to conversation {conversationId} as Client 1");
        await ProcessMessageQueue(e.Client.Guid, conversationId);
    }
    else if (conversation.Client2 == Guid.Empty && !clientAlreadyInConversation)
    {
        conversation.Client2 = e.Client.Guid;
        conversations[conversationId] = conversation;
        Console.WriteLine($"Client {e.Client.Guid} connected to conversation {conversationId} as Client 2");
        await ProcessMessageQueue(e.Client.Guid, conversationId);
    }

    if (e.Data.Count <= 64)
    {
        Console.WriteLine("Only conversation ID received");
        return;
    }

    if (conversation.Client1 == Guid.Empty)
    {
        Console.WriteLine($"Client 2 is waiting for Client 1 to connect to conversation {conversationId}");
        conversation.MessageQueue1.Enqueue(e.Data);
        return;
    }
    else if (conversation.Client2 == Guid.Empty)
    {
        Console.WriteLine($"Client 1 is waiting for Client 2 to connect to conversation {conversationId}");
        conversation.MessageQueue2.Enqueue(e.Data);
        return;
    }

    if (conversation.Client1 == e.Client.Guid)
    {
        Console.WriteLine($"Sending message from Client 1 to Client 2 in conversation {conversationId}");
        _ = await webSocketServer.SendAsync(conversation.Client2, e.Data);
    }
    else if (conversation.Client2 == e.Client.Guid)
    {
        Console.WriteLine($"Sending message from Client 2 to Client 1 in conversation {conversationId}");
        _ = await webSocketServer.SendAsync(conversation.Client1, e.Data);
    }
    else
    {
        Console.WriteLine($"Client {e.Client.Guid} is not part of the conversation {conversationId}");
        webSocketServer.DisconnectClient(e.Client.Guid);
    }
}

async Task ProcessMessageQueue(Guid clientId, string conversationId)
{
    if (!conversations.TryGetValue(conversationId, out Conversation conversation))
    {
        return;
    }

    if (conversation.Client1 == clientId)
    {
        int tries = 0;

        while (conversation.MessageQueue1.TryPeek(out ArraySegment<byte> message))
        {
            Console.WriteLine($"Processing queued message from Client 2 to Client 1 in conversation {conversationId}");

            tries++;

            if (tries > conversation.MessageQueue1.Count * 2)
            {
                break;
            }

            if (await webSocketServer.SendAsync(conversation.Client1, message))
            {
                _ = conversation.MessageQueue1.TryDequeue(out _);
                tries = 0;
            }
        }
    }
    else if (conversation.Client2 == clientId)
    {
        int tries = 0;

        while (conversation.MessageQueue2.TryPeek(out ArraySegment<byte> message))
        {
            Console.WriteLine($"Processing queued message from Client 1 to Client 2 in conversation {conversationId}");

            tries++;

            if (tries > conversation.MessageQueue2.Count * 2)
            {
                break;
            }

            if (await webSocketServer.SendAsync(conversation.Client2, message))
            {
                _ = conversation.MessageQueue2.TryDequeue(out _);
                tries = 0;
            }
        }
    }
}

public record struct Conversation(Guid Client1, Guid Client2, ConcurrentQueue<ArraySegment<byte>> MessageQueue1, ConcurrentQueue<ArraySegment<byte>> MessageQueue2);