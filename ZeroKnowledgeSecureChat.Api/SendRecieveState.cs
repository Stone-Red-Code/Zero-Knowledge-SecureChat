namespace ZeroKnowledgeSecureChat.Api;

public enum SendReceiveState
{
    None,
    WaitForSendPermission,
    WaitForMessage,
    WaitForMessageAccept,
    WaitForPong,
    SendAllowed,
    PongReceived,
    MessageAccepted
}