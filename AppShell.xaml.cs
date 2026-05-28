using Podify.Pages;

namespace Podify;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("podcast", typeof(PodcastDetailPage));
        Routing.RegisterRoute("episode", typeof(EpisodeDetailPage));
        Routing.RegisterRoute("queue", typeof(QueuePage));
        Routing.RegisterRoute("downloads", typeof(DownloadsPage));
    }
}
