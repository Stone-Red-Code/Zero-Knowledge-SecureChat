using CommunityToolkit.Maui.Alerts;

using WatsonWebsocket;

namespace ZeroKnowledgeSecureChat;

public partial class App : Application
{
    public static WatsonWsClient WebSocketClient { get; } = new WatsonWsClient(new Uri("ws://localhost:9000"));

    public App()
    {
        InitializeComponent();
        WebSocketClient.ServerDisconnected += WebSocketClient_ServerDisconnected;

        _ = WebSocketClient.StartAsync();
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
        MainPageDataContext mainPageDataContext = (MainPageDataContext)window.Page!.BindingContext;

        _ = Toast.Make("Saving conversations...").Show();
        AsyncHelper.RunSync(mainPageDataContext.Save);
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