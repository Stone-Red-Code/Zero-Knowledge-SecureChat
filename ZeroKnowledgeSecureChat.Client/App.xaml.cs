using CommunityToolkit.Maui.Alerts;

using System.Security.Cryptography;
using System.Text.Json;

using WatsonWebsocket;

using ZeroKnowledgeSecureChat.Api;

namespace ZeroKnowledgeSecureChat;

public partial class App : Application
{
    private static readonly string savePath = Path.Combine(FileSystem.Current.AppDataDirectory, "conversations.json.aes");
    private static readonly SemaphoreSlim saveLoadSemaphore = new SemaphoreSlim(1, 1);

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions
    {
        Converters = { new ConcurrentBagJsonConverter<Message>() }
    };

    public static WatsonWsClient WebSocketClient { get; } = new WatsonWsClient(new Uri("ws://localhost:9000"));

    public App()
    {
        InitializeComponent();
        WebSocketClient.ServerDisconnected += WebSocketClient_ServerDisconnected;
    }

    public static async Task Save()
    {
        await saveLoadSemaphore.WaitAsync();

        MainPageDataContext mainPageDataContext = (MainPageDataContext)Application.Current!.Windows[0].Page!.BindingContext;
        string json = await Task.Run(() => JsonSerializer.Serialize(mainPageDataContext.ChatClients.Select(c => c.ChatClient.ChatClientState), App.JsonSerializerOptions));

        string? encryptionKey = await SecureStorage.Default.GetAsync("encryption_key");
        string? iv = await SecureStorage.Default.GetAsync("encryption_iv");

        mainPageDataContext.LoadingText = "Saving conversations...";
        mainPageDataContext.Loading = true;

        using Aes aes = Aes.Create();

        if (encryptionKey is null || iv is null)
        {
            await SecureStorage.Default.SetAsync("encryption_key", Convert.ToBase64String(aes.Key));
            await SecureStorage.Default.SetAsync("encryption_iv", Convert.ToBase64String(aes.IV));
        }
        else
        {
            aes.Key = Convert.FromBase64String(encryptionKey);
            aes.IV = Convert.FromBase64String(iv);
        }

        using FileStream fileStream = new FileStream(savePath, FileMode.Create);
        using CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using StreamWriter streamWriter = new StreamWriter(cryptoStream);

        await streamWriter.WriteAsync(json);

        mainPageDataContext.Loading = false;

        _ = saveLoadSemaphore.Release();
    }

    public static async Task Load()
    {
        MainPageDataContext mainPageDataContext = (MainPageDataContext)Current!.Windows[0].Page!.BindingContext;

        string? encryptionKey = await SecureStorage.Default.GetAsync("encryption_key");
        string? iv = await SecureStorage.Default.GetAsync("encryption_iv");

        if (encryptionKey is null || iv is null || !File.Exists(savePath))
        {
            return;
        }

        await saveLoadSemaphore.WaitAsync();

        mainPageDataContext.LoadingText = "Loading conversations...";
        mainPageDataContext.Loading = true;

        using Aes aes = Aes.Create();

        aes.Key = Convert.FromBase64String(encryptionKey);
        aes.IV = Convert.FromBase64String(iv);

        using FileStream fileStream = new FileStream(savePath, FileMode.Open);
        using CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using StreamReader streamReader = new StreamReader(cryptoStream);

        string json = await streamReader.ReadToEndAsync();

        IEnumerable<ChatClientState>? chatClientStates;

        try
        {
            chatClientStates = JsonSerializer.Deserialize<IEnumerable<ChatClientState>>(json, App.JsonSerializerOptions);
        }
        catch
        {
            mainPageDataContext.Loading = false;
            _ = saveLoadSemaphore.Release();
            return;
        }

        if (chatClientStates is null)
        {
            mainPageDataContext.Loading = false;
            _ = saveLoadSemaphore.Release();
            return;
        }

        foreach (ChatClientState chatClientState in chatClientStates)
        {
            mainPageDataContext.ChatClients.Add(new ChatClientViewModel(new WebSocketChatClient(chatClientState, App.WebSocketClient)));
        }

        mainPageDataContext.Loading = false;
        _ = saveLoadSemaphore.Release();
    }

    protected override async void OnStart()
    {
        await WebSocketClient.StartAsync();
        await Load();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window;

#if ANDROID || IOS
        window = new Window(new NavigationPage(new MobileMainPage()));
#else
        window = new Window(new MainPage());
#endif

        window.Destroying += Window_Destroying;

        return window;
    }

    private static void Window_Destroying(object? sender, EventArgs e)
    {
        WebSocketClient.ServerDisconnected -= WebSocketClient_ServerDisconnected;
        WebSocketClient.Dispose();

        Window window = (Window)sender!;

        _ = Toast.Make("Saving conversations...").Show();
        AsyncHelper.RunSync(Save);
        _ = Toast.Make("Conversations saved!").Show();
    }

    private static void WebSocketClient_ServerDisconnected(object? sender, EventArgs e)
    {
        _ = WebSocketClient.StartAsync();
    }
}

public static class AsyncHelper
{
    private static readonly TaskFactory _taskFactory = new
        TaskFactory(CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

    public static TResult RunSync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        return _taskFactory
                .StartNew(func, cancellationToken)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
    }

    public static void RunSync(Func<Task> func, CancellationToken cancellationToken = default)
    {
        _taskFactory
                .StartNew(func, cancellationToken)
                .Unwrap()
                .GetAwaiter()
        .GetResult();
    }
}