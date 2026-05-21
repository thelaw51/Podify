using PodcastApp.Pages;

namespace PodcastApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("podcast", typeof(PodcastDetailPage));
    }
}
