using Podify.ViewModels;

namespace Podify.Pages;

public partial class PlayerPage : ContentPage
{
    private readonly PlayerViewModel _vm;

    public PlayerPage(PlayerViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Player.AttachMediaElement(Media);
    }
}
