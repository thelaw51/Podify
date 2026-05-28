using Podify.ViewModels;

namespace Podify.Pages;

public partial class DownloadsPage : ContentPage
{
    private readonly DownloadsViewModel _vm;

    public DownloadsPage(DownloadsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadAsync();
    }
}
