using Podify.Pages;

namespace Podify;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("podcast", typeof(PodcastDetailPage));
    }
}
