using FluentResults;

using Syncfusion.Maui.Chat;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using ZeroKnowledgeSecureChat.Api;

namespace ZeroKnowledgeSecureChat;

public partial class ChatClientViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int queuedMessages = 0;
    private int queuedMessagesMax = 0;
    private string errorMessage = string.Empty;
    private SendReceiveState sendReceiveState = SendReceiveState.None;

    public ICommand SendMessage { get; }
    public WebSocketChatClient ChatClient { get; }

    public Author CurrentUser { get; } = new Author()
    {
        Name = "You"
    };

    public ObservableCollection<object> Messages { get; }

    public string ServerConnectionStatus => $"Server: {(ChatClient.IsConnected ? "Connected" : "Disconnected")}";
    public string State => $"State: {SendReceiveState}";
    public string KeyBytesUsed => $"Key bytes used: {1 - (ChatClient.CurrentKeyLength / (double)ChatClient.InitialKeyLength):P2}";

    public int QueuedMessages => queuedMessages;
    public int QueuedMessagesMax => queuedMessagesMax == 0 ? 0 : queuedMessagesMax + 1;
    public int QueuedMessagesRemaining => queuedMessagesMax == 0 ? 0 : queuedMessagesMax - queuedMessages + 1;

    public string LastMessage => GetLastMessage();

    public DateTime? LastMessageDateTime => Messages.LastOrDefault() is TextMessage textMessage ? textMessage.DateTime : null;

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            errorMessage = value;
            OnPropertyChanged();
        }
    }

    public SendReceiveState SendReceiveState
    {
        get => sendReceiveState;
        set
        {
            sendReceiveState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(State));
        }
    }

    public ChatClientViewModel(WebSocketChatClient chatClient)
    {
        ChatClient = chatClient;

        Messages = [.. chatClient.Messages.Select(CreateTextMessage)];

        chatClient.SendReceiveStateChanged += (s, e) => SendReceiveState = e;
        chatClient.ErrorReceived += (s, e) => ErrorMessage = e;
        chatClient.MessageReceived += (s, e) => Application.Current?.Dispatcher.Dispatch(() => { Messages.Add(CreateTextMessage(e)); UpdateMessageRelatedProperties(); _ = App.Save(); });

        SendMessage = new Command(async (object? args) =>
        {
            ErrorMessage = string.Empty;

            if (args is not SendMessageEventArgs e || e.Message is null)
            {
                ErrorMessage = "No message to send";
                return;
            }

            _ = Interlocked.Increment(ref queuedMessages);
            _ = Interlocked.Exchange(ref queuedMessagesMax, Math.Max(queuedMessages, queuedMessagesMax));

            UpdateMessageRelatedProperties();

            e.Message.Text = e.Message.Text.Trim();

            Result result = await ChatClient.SendMessage(e.Message.Text);

            _ = Interlocked.Decrement(ref queuedMessages);
            if (queuedMessages == 0)
            {
                _ = Interlocked.Exchange(ref queuedMessagesMax, 0);
            }

            UpdateMessageRelatedProperties();

            if (result.IsFailed)
            {
                ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Message));

                bool? answer = await Application.Current!.Windows[0].Page!.DisplayAlert("Sending message failed!", e.Message.Text, "Resend Message", "Copy to Clipboard");

                if (answer == true)
                {
                    SendMessage!.Execute(e);
                }
                else
                {
                    await Clipboard.SetTextAsync(e.Message.Text);
                    e.Handled = true;
                    _ = Messages.Remove(e.Message);
                }

                UpdateMessageRelatedProperties();
            }
            else
            {
                ErrorMessage = string.Empty;
                await App.Save();
            }
        });
    }

    public void OnPropertyChanged([CallerMemberName] string name = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private TextMessage CreateTextMessage(Message message)
    {
        return new TextMessage()
        {
            Text = message.Content.Trim(),
            DateTime = message.Timestamp,
            Author = message.Author is null ? CurrentUser : new Author()
            {
                Name = message.Author
            }
        };
    }

    private string GetLastMessage()
    {
        string message = Messages.LastOrDefault() is TextMessage textMessage ? textMessage.Text : string.Empty;

        string firstLine = new string([.. message.TakeWhile(c => c is not '\n' and not '\r')]);

        return firstLine;
    }

    private void UpdateMessageRelatedProperties()
    {
        OnPropertyChanged(nameof(QueuedMessages));
        OnPropertyChanged(nameof(QueuedMessagesMax));
        OnPropertyChanged(nameof(QueuedMessagesRemaining));
        OnPropertyChanged(nameof(KeyBytesUsed));
        OnPropertyChanged(nameof(LastMessage));
        OnPropertyChanged(nameof(LastMessageDateTime));
    }
}