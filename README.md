# Zero-Knowledge SecureChat (ZKS)

## Not Just Private, Uncrackable

ZKS is a highly secure chat system utilizing the **One-Time Pad (OTP)** encryption method. It ensures absolute confidentiality by generating new encryption keys for each message, making it theoretically unbreakable if implemented correctly.

## How It Works

ZKS follows a strict protocol for secure communication between clients:

1. **Key Generation & Exchange:**
   - When creating a chat session one client generates a random OTP key & signing key.
   - There is no built-in key exchange mechanism because every known key exchange method is less secure than OTP itself. Instead, the initial OTP key & singing key should be exchanged over an external secure channel.

2. **Handshake & Synchronization:**
   - A **PING** message is sent to verify the other client's availability.
   - If the recipient is online, it responds with a **PONG**.

3. **Message Transmission:**
   - The sender requests permission with a **MESSAGE_REQUEST**.
   - The recipient can accept (**MESSAGE_REQUEST_ACCEPT**) or deny (**MESSAGE_REQUEST_DENY**).
   - If accepted, the encrypted message is sent along with a newly generated OTP key.
   - Upon successful decryption, the recipient acknowledges with a **MESSAGE_RECEIVED**.

4. **Message Security:**
   - Each message is encrypted using an OTP key before transmission.
   - A **new OTP key is embedded inside the encrypted message**, ensuring forward secrecy.
   - HMAC (Hash-based Message Authentication Code) is used to verify integrity.

## Protocol Format

Each transmitted packet follows this strict structure:

```text
[HMAC (64 bytes)][Version (1 byte)][Type (1 byte)][Timestamp (8 bytes)][Data]
```

- **HMAC (64 bytes):** Ensures data integrity and authenticity.
- **Version (1 byte):** Defines protocol version.
- **Type (1 byte):** Specifies message type (e.g., PING, MESSAGE, ERROR).
- **Timestamp (8 bytes):** Prevents replay attacks by ensuring messages are recent.
- **Data:** The actual message payload (if applicable).

### Message Format

```text
[Content length (2 bytes)][Content][New OTP Key ((Current key lenght - Content length - 2) bytes)]
```

- **Content length:** Specifies the length of the message.
- **Content:** The encrypted chat message.
- **New OTP Key:** Used for the next message, ensuring ongoing security.

## Usage

There is a client implementation in the `ZeroKnowledgeSecureChat.Client` project, but you can also implement your own client using the provided API.

To compile and run the project, you need to have the .NET 9 SDK installed and need Visual Studio 2022 for the client project.

Simply run the `ZeroKnowledgeSecureChat.Server` project and then the `ZeroKnowledgeSecureChat.Client` project to use the chat system.

## Example

1. Client A sends a **PING**.
2. Client B replies with **PONG**.
3. Client A sends a **MESSAGE_REQUEST**.
4. Client B accepts (**MESSAGE_REQUEST_ACCEPT**).
5. Client A encrypts and sends the message with a **new OTP key**.
6. Client B decrypts, updates its key, and acknowledges receipt with **MESSAGE_RECEIVED**.

## Clients

- This repository contains a `ZeroKnowledgeSecureChat.Api` C# project that provides a simple API for sending and receiving messages.
- To use it you have to implement the abstract class 'ChatClient' and override the `TransmitData` method and call the `ProcessData` method.
- You can find an example implementation in the `ZeroKnowledgeSecureChat.Client` project.

## Third-Party Licenses

- [FluentResults](https://github.com/altmann/FluentResults) - [MIT](https://github.com/altmann/FluentResults/blob/master/LICENSE)
- [Watson Websocket](https://github.com/jchristn/WatsonWebsocket) - [MIT](https://github.com/jchristn/WatsonWebsocket/blob/master/LICENSE.md)
- [Syncfusion Toolkit for .NET MAUI](https://github.com/syncfusion/maui-toolkit) - [MIT](https://github.com/syncfusion/maui-toolkit/blob/main/LICENSE.txt)
- [Syncfusion](https://www.syncfusion.com/) - [Custom](https://www.nuget.org/packages/Syncfusion.Maui.Chat/28.2.9/License)
