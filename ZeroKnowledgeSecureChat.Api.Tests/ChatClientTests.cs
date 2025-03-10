using FluentResults;

using System.Diagnostics;

namespace ZeroKnowledgeSecureChat.Api.Tests;

[TestClass]
public sealed class ChatClientTests
{
    [TestMethod]
    public async Task SendMessage()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey);

        Message? receivedMessage = null;

        client1.SendData += async (sender, data) => await client2.ReceiveData(data);
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        client2.MessageReceived += (sender, message) => receivedMessage = message;

        string messageContent = "Hello, world!";
        Result result = await client1.SendMessage(messageContent);

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(receivedMessage);

        Assert.AreEqual(messageContent, receivedMessage.Content);
        Assert.AreEqual(client2.Name, receivedMessage.Author);

        Message? lastClient1Message = client1.Messages.LastOrDefault();
        Message? lastClient2Message = client2.Messages.LastOrDefault();

        Assert.IsNotNull(lastClient1Message);
        Assert.IsNotNull(lastClient2Message);

        Assert.AreEqual(lastClient1Message.Content, receivedMessage.Content);
        Assert.AreSame(lastClient2Message, receivedMessage);
    }

    [TestMethod]
    public async Task SendMessageTimeout()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        Message? receivedMessage = null;

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey);

        client1.SendData += async (sender, data) => { await Task.Delay(10000); await client2.ReceiveData(data); };
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        client2.MessageReceived += (sender, message) => receivedMessage = message;

        string messageContent = "Hello, world!";

        Result result = await client1.SendMessage(messageContent);

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsFailed);
        Assert.IsNull(receivedMessage);
    }

    [TestMethod]
    public async Task SendMessageIncorrectInitialKey()
    {
        byte[] initialKey1 = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] initialKey2 = EncryptionUtilities.GenerateRandomKey(1000);

        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        Message? receivedMessage = null;

        TestChatClient client1 = new TestChatClient("Client 1", initialKey1, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey2, signingKey);

        client1.SendData += async (sender, data) => await client2.ReceiveData(data);
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        client2.MessageReceived += (sender, message) => receivedMessage = message;

        string messageContent = "Hello, world!";

        Result result = await client1.SendMessage(messageContent);

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsFailed);
        Assert.IsNull(receivedMessage);
    }

    [TestMethod]
    public async Task SendMessageIncorrectSigningKey()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);

        byte[] signingKey1 = EncryptionUtilities.GenerateRandomKey(512);
        byte[] signingKey2 = EncryptionUtilities.GenerateRandomKey(512);

        Message? receivedMessage = null;

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey1);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey2);

        client1.SendData += async (sender, data) => await client2.ReceiveData(data);
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        client2.MessageReceived += (sender, message) => receivedMessage = message;

        string messageContent = "Hello, world!";

        Result result = await client1.SendMessage(messageContent);

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsFailed);
        Assert.IsNull(receivedMessage);
    }

    [TestMethod]
    public async Task SendMessageIncorrectData()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        Message? receivedMessage = null;

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey);

        client1.SendData += async (sender, data) => { data[^1] = 0; await client2.ReceiveData(data); };
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        client2.MessageReceived += (sender, message) => receivedMessage = message;

        string messageContent = "Hello, world!";

        Result result = await client1.SendMessage(messageContent);

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsFailed);
        Assert.IsNull(receivedMessage);
    }

    [TestMethod]
    public async Task SendPing()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey);

        client1.SendData += async (sender, data) => await client2.ReceiveData(data);
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        Result result = await client1.SendPing();

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task SendPingTimeout()
    {
        byte[] initialKey = EncryptionUtilities.GenerateRandomKey(1000);
        byte[] signingKey = EncryptionUtilities.GenerateRandomKey(512);

        TestChatClient client1 = new TestChatClient("Client 1", initialKey, signingKey);
        TestChatClient client2 = new TestChatClient("Client 2", initialKey, signingKey);

        client1.SendData += async (sender, data) => { await Task.Delay(10000); await client2.ReceiveData(data); };
        client2.SendData += async (sender, data) => await client1.ReceiveData(data);

        Result result = await client1.SendPing();

        Trace.WriteLine(string.Join(", ", result.Errors.Select(x => x.Message)));

        Assert.IsTrue(result.IsFailed);
    }
}