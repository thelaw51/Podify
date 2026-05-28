using Podify.ViewModels;

namespace Podify.Pages;

public partial class QueuePage : ContentPage
{
    private readonly QueueViewModel _vm;

    public QueuePage(QueueViewModel vm)
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
