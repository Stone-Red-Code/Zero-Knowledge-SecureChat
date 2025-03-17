namespace ZeroKnowledgeSecureChat;

public partial class MobileChatPage : ContentPage
{
    public MobileChatPage(ChatClientViewModel chatClient)
    {
        InitializeComponent();

        BindingContext = chatClient;
    }
}