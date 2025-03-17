using Microsoft.Windows.AppLifecycle;

using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ZeroKnowledgeSecureChat.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private const string InstanceKey = "09f7a83f-77c6-4983-ab41-92abe06d5e3c";

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        AppInstance singleInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!singleInstance.IsCurrent)
        {
            AppInstance currentInstance = AppInstance.GetCurrent();
            AppActivationArguments args = currentInstance.GetActivatedEventArgs();
            singleInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();

            // 2. close this instance
            Process.GetCurrentProcess().Kill();
            return;
        }

        singleInstance.Activated += OnAppInstanceActivated;

        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    private static void OnAppInstanceActivated(object? sender, AppActivationArguments e)
    {
        _ = Microsoft.Maui.Controls.Application.Current!.Dispatcher.Dispatch(() =>
        {
            IWindow window = Current.Application.Windows[0];
            Current.Application.ActivateWindow(window);
        });
    }
}