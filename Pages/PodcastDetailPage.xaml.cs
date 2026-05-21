using PodcastApp.ViewModels;

namespace PodcastApp.Pages;

public partial class PodcastDetailPage : ContentPage
{
    public PodcastDetailPage(PodcastDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
