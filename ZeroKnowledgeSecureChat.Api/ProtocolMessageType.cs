namespace ZeroKnowledgeSecureChat.Api;

internal enum ProtocolMessageType : byte
{
    Ping = 0,
    Pong = 1,
    Message = 2,
    MessageRequest = 3,
    MessageRequestAccept = 4,
    MessageRequestDeny = 5,
    MessageRequestCancel = 6,
    MessageReceived = 7,
    MessageError = 8,
    Error = 255,
}