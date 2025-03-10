namespace ZeroKnowledgeSecureChat;

public partial class MobileMainPage : ContentPage
{
    private readonly MainPageDataContext dataContext = new();
    private readonly Dictionary<ChatClientViewModel, MobileChatPage> cachedPages = [];

    public MobileMainPage()
    {
        InitializeComponent();

        BindingContext = dataContext;
    }

    private void ConversationsListView_ItemTapped(object sender, Syncfusion.Maui.ListView.ItemTappedEventArgs e)
    {
        if (e.DataItem is not ChatClientViewModel chatClientViewModel)
        {
            return;
        }

        if (cachedPages.TryGetValue(chatClientViewModel, out MobileChatPage? page))
        {
            _ = Navigation.PushAsync(page, true);
            return;
        }

        _ = Navigation.PushAsync(new MobileChatPage(chatClientViewModel), true);
    }

    private async void ContentPage_Loaded(object sender, EventArgs e)
    {
        if (dataContext.ChatClients.Count > 0)
        {
            return;
        }

        await dataContext.Load();
    }

    private async void SfButton_Clicked(object sender, EventArgs e)
    {
        await dataContext.Save();
    }
}