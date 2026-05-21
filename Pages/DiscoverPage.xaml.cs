using PodcastApp.ViewModels;

namespace PodcastApp.Pages;

public partial class DiscoverPage : ContentPage
{
    private readonly DiscoverViewModel _vm;

    public DiscoverPage(DiscoverViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
