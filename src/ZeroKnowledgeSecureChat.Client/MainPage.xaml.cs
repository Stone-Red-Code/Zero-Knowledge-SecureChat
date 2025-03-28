﻿namespace ZeroKnowledgeSecureChat;

public partial class MainPage : ContentPage
{
    private readonly MainPageDataContext dataContext = new();

    public MainPage()
    {
        InitializeComponent();

        BindingContext = dataContext;
    }

    private void ContentPage_Loaded(object sender, EventArgs e)
    {
#if WINDOWS
        Microsoft.UI.Xaml.FrameworkElement? nativeView = conversationsListView.Handler!.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if (nativeView is not null)
        {
            nativeView.UseSystemFocusVisuals = false;
        }

        nativeView = sfChat.Handler!.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if (nativeView is not null)
        {
            nativeView.UseSystemFocusVisuals = false;
        }
#endif
    }

    private async void SfButton_Clicked(object sender, EventArgs e)
    {
        await App.Save();
    }
}