using Podify.ViewModels;

namespace Podify.Pages;

public partial class PodcastDetailPage : ContentPage
{
    public PodcastDetailPage(PodcastDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
