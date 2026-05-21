using PodcastApp.ViewModels;

namespace PodcastApp.Pages;

public partial class SubscriptionsPage : ContentPage
{
    private readonly SubscriptionsViewModel _vm;

    public SubscriptionsPage(SubscriptionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
