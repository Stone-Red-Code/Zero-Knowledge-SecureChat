using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;

using CuteUtils;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

using ZeroKnowledgeSecureChat.Api;

namespace ZeroKnowledgeSecureChat;

internal partial class MainPageDataContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        Converters = { new ConcurrentBagJsonConverter<Message>() }
    };

    private ChatClientViewModel? selectedChatClient;
    private bool newConversationDialogIsOpen;
    private bool loading;
    private double loadingProgress;
    private string loadingText = "Loading...";

    public ObservableCollection<ChatClientViewModel> ChatClients { get; } = [];

    public string NewConversationName { get; set; } = string.Empty;

    public ChatClientViewModel? SelectedChatClient
    {
        get => selectedChatClient;
        set
        {
            selectedChatClient = value;
            OnPropertyChanged();
        }
    }

    public bool NewConversationDialogIsOpen
    {
        get => newConversationDialogIsOpen;
        set
        {
            newConversationDialogIsOpen = value;
            OnPropertyChanged();
        }
    }

    public bool Loading
    {
        get => loading;
        set
        {
            if (value)
            {
                LoadingProgress = 0;
            }

            loading = value;
            OnPropertyChanged();
        }
    }

    public double LoadingProgress
    {
        get => loadingProgress;
        set
        {
            loadingProgress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingIndeterminate));
        }
    }

    public string LoadingText
    {
        get => loadingText;
        set
        {
            loadingText = value;
            OnPropertyChanged();
        }
    }

    public bool LoadingIndeterminate => LoadingProgress is <= 0 or >= 1;

    public ICommand OpenNewConversationDialog => new Command(() => NewConversationDialogIsOpen = true);

    public ICommand CreateNewConversation => new Command(async () =>
    {
        if (string.IsNullOrWhiteSpace(NewConversationName))
        {
            return;
        }

        ChatClientViewModel chatClient = new ChatClientViewModel(new WebSocketChatClient(NewConversationName, 10000000, App.WebSocketClient));
        ChatClients.Add(chatClient);

        using MemoryStream stream = new MemoryStream(Encoding.Default.GetBytes(JsonSerializer.Serialize(chatClient.ChatClient.ChatClientState, jsonSerializerOptions)));

        FileSaverResult fileSaverResult = await FileSaver.Default.SaveAsync($"{NewConversationName.ToFileName()}.zksc", stream);
        if (fileSaverResult.IsSuccessful)
        {
            await Toast.Make($"The file was saved successfully to location: {fileSaverResult.FilePath}").Show();
        }
        else
        {
            await Toast.Make($"The file was not saved successfully with error: {fileSaverResult.Exception.Message}").Show();
        }

        SelectedChatClient = chatClient;
        NewConversationDialogIsOpen = false;
        NewConversationName = string.Empty;

        await Save();
    });

    public ICommand ImportConversation => new Command(async () =>
    {
        FileResult? filePickerResult = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select a conversation file to import",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>()
            {
                {DevicePlatform.WinUI, [".zksc"]},
                {DevicePlatform.macOS, ["zksc"]},
                {DevicePlatform.iOS, ["zksc"]},
                {DevicePlatform.Android, ["*/*"]},
            })
        });

        if (filePickerResult is null)
        {
            return;
        }

        using Stream stream = await filePickerResult.OpenReadAsync();

        Loading = true;
        LoadingText = "Importing conversation...";

        ChatClientState? chatClientState = await JsonSerializer.DeserializeAsync<ChatClientState>(stream, jsonSerializerOptions);

        if (chatClientState is null)
        {
            return;
        }

        ChatClientViewModel chatClient = new ChatClientViewModel(new WebSocketChatClient(chatClientState, App.WebSocketClient));

        ChatClients.Add(chatClient);

        SelectedChatClient = chatClient;
        NewConversationDialogIsOpen = false;
        NewConversationName = string.Empty;

        Loading = false;

        await Toast.Make("The conversation was imported successfully").Show();

        await Save();
    });

    public void OnPropertyChanged([CallerMemberName] string name = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public async Task Load()
    {
        Loading = true;
        LoadingText = "Loading conversations...";

        int count = 0;
        string? chunk;
        StringBuilder json = new StringBuilder();

        string? chunkCountString = await SecureStorage.Default.GetAsync("conversations_chunk_count");

        if (!double.TryParse(chunkCountString, out double chunkCount))
        {
            chunkCount = 1;
        }

        do
        {
            chunk = await SecureStorage.Default.GetAsync($"conversations_{count}");
            _ = json.Append(chunk);
            count++;
            if (count % 50 == 0)
            {
                LoadingProgress = count / chunkCount;
            }
        } while (chunk is not null);

        if (json.Length == 0)
        {
            Loading = false;
            return;
        }

        IEnumerable<ChatClientState>? chatClientStates;

        try
        {
            chatClientStates = JsonSerializer.Deserialize<IEnumerable<ChatClientState>>(json.ToString(), jsonSerializerOptions);
        }
        catch
        {
            Loading = false;
            return;
        }

        if (chatClientStates is null)
        {
            Loading = false;
            return;
        }

        foreach (ChatClientState chatClientState in chatClientStates)
        {
            ChatClientViewModel chatClient = new ChatClientViewModel(new WebSocketChatClient(chatClientState, App.WebSocketClient));
            ChatClients.Add(chatClient);
        }

        Loading = false;
    }

    public async Task Save()
    {
        Loading = true;
        LoadingText = "Saving conversations...";

        string json = await Task.Run(() => JsonSerializer.Serialize(ChatClients.Select(c => c.ChatClient.ChatClientState), jsonSerializerOptions));
        int count = 0;

#if WINDOWS
        int chunkSize = 4096;
#else
        int chunkSize = 409600;
#endif

        double approximateChunkCount = json.Length / (double)chunkSize;

        SecureStorage.Default.RemoveAll();

        while (json.Length > 0)
        {
            int startIndex = Math.Min(count * chunkSize, json.Length);
            int length = Math.Min(chunkSize, json.Length - startIndex);

            if (length <= 0)
            {
                break;
            }

            string chunk = json.Substring(startIndex, length);
            await SecureStorage.Default.SetAsync($"conversations_{count}", chunk);
            count++;

            if (count % 50 == 0)
            {
                LoadingProgress = count / approximateChunkCount;
            }
        }

        await SecureStorage.Default.SetAsync($"conversations_chunk_count", count.ToString());

        Loading = false;
    }
}