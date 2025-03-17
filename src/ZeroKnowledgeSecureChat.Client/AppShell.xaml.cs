namespace ZeroKnowledgeSecureChat;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

#if ANDROID || IOS
        shellContent.ContentTemplate = new DataTemplate(typeof(MobileMainPage));
        shellContent.Route = "MobileMainPage";
#else
        shellContent.ContentTemplate = new DataTemplate(typeof(MainPage));
        shellContent.Route = "MainPage";
#endif
    }
}