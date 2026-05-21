using PodcastApp.Services;

namespace PodcastApp;

public partial class App : Application
{
    private readonly PlayerService _player;

    public App(PlayerService player)
    {
        InitializeComponent();
        _player = player;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new AppShell());

    protected override async void OnSleep()
    {
        base.OnSleep();
        await _player.SaveCurrentPositionAsync();
    }
}
