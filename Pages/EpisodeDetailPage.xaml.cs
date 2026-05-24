using Podify.ViewModels;

namespace Podify.Pages;

public partial class EpisodeDetailPage : ContentPage
{
    public EpisodeDetailPage(EpisodeDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
