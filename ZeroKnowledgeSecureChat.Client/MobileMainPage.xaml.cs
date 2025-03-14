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

    private void SfButton_Clicked(object sender, EventArgs e)
    {
        _ = App.Save();
    }
}